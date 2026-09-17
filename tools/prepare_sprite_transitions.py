from pathlib import Path
import json
import numpy as np
from PIL import Image
# Developer-only dependency. Nothing is installed on the friend's computer.
import cv2
from scipy.ndimage import distance_transform_edt
cv2.setNumThreads(2)
root=Path(__file__).resolve().parents[1]
out=root/'assets/sprites/smooth';out.mkdir(parents=True,exist_ok=True)
atlas=Image.open(root/'assets/sprites/cat-spritesheet.png').convert('RGBA')
W,H=192,208
ys,xs=np.mgrid[0:H,0:W].astype(np.float32)

def image(row,frame):
    return np.asarray(atlas.crop((frame*W,row*H,(frame+1)*W,(row+1)*H))).astype(np.float32)/255

def remap(value,x,y):
    return cv2.remap(value,x.astype(np.float32),y.astype(np.float32),cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT)

def flow(a,b):
    def gray(v):
        return np.uint8(np.clip((cv2.cvtColor(v[:,:,:3],cv2.COLOR_RGB2GRAY)*0.7+0.3)*v[:,:,3]*255,0,255))
    dis=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    dis.setFinestScale(0);dis.setPatchStride(3);dis.setGradientDescentIterations(40);dis.setVariationalRefinementIterations(12)
    value=dis.calc(gray(a),gray(b),None)
    idx=distance_transform_edt(a[:,:,3]<0.15,return_distances=False,return_indices=True)
    value=value[idx[0],idx[1]]
    return cv2.GaussianBlur(value,(0,0),3)

def interpolate(a,b,f,g,t):
    if t==0:return a
    ax=xs-t*f[:,:,0];ay=ys-t*f[:,:,1]
    bx=xs-(1-t)*g[:,:,0];by=ys-(1-t)*g[:,:,1]
    ap=a.copy();bp=b.copy();ap[:,:,:3]*=ap[:,:,3:4];bp[:,:,:3]*=bp[:,:,3:4]
    wa=remap(ap,ax,ay);wb=remap(bp,bx,by)
    mixed=(1-t)*wa+t*wb
    mixed[:,:,:3]/=np.maximum(mixed[:,:,3:4],1/255)
    def sdf(alpha):
        inside=alpha>0.5
        return distance_transform_edt(inside)-distance_transform_edt(~inside)
    sd=(1-t)*sdf(wa[:,:,3])+t*sdf(wb[:,:,3])
    mixed[:,:,3]=np.clip((sd+1)/2,0,1)
    mask=np.uint8(mixed[:,:,3]>0.12)
    total,labels,stats,_=cv2.connectedComponentsWithStats(mask,8)
    if total>1:
        keep=int(1+np.argmax(stats[1:,cv2.CC_STAT_AREA]))
        valid=cv2.dilate(np.uint8(labels==keep),np.ones((3,3),np.uint8))
        mixed[valid==0]=0
    mixed[mixed[:,:,3]<1/255]=0
    return np.clip(mixed,0,1)


NAMES=['idle','walk-right','walk-left','wave','stretch','groom','look','lie-down','sleep']
COUNTS=[6,8,8,4,5,8,6,6,6]
STEPS=[4,4,4,16,20,16,16,8,16]
LOOPS={0,1,2,8}
metadata={'source':'../cat-spritesheet.png','method':'Bidirectional DIS flow, smooth displacement, premultiplied color and signed-distance alpha','frameWidth':W,'frameHeight':H,'columns':8,'actions':[]}
for row,name in enumerate(NAMES):
    count,steps=COUNTS[row],STEPS[row]
    keys=[image(row,i) for i in range(count)]
    frames=[]
    for i,a in enumerate(keys):
        if row not in LOOPS and i==count-1:
            frames.append(np.uint8(np.rint(a*255)))
            break
        b=keys[(i+1)%count]
        f,g=flow(a,b),flow(b,a)
        for k in range(steps):
            frame=np.uint8(np.rint(interpolate(a,b,f,g,k/steps)*255))
            if np.any(frame[[0,-1],:,3]>=32) or np.any(frame[:,[0,-1],3]>=32):
                raise ValueError(name+' frame touches the canvas border')
            if np.count_nonzero(frame[:,:,3]>200)<1000:
                raise ValueError(name+' frame lost its foreground')
            frames.append(frame)
    sheet=Image.new('RGBA',(W*8,H*((len(frames)+7)//8)),(0,0,0,0))
    for i,frame in enumerate(frames):
        sheet.paste(Image.fromarray(frame),(i%8*W,i//8*H))
    sheet.save(out/(name+'.png'),optimize=True)
    def premul(a):return a[:,:,:3].astype(float)*a[:,:,3:4]/255
    changes=[float(np.abs(premul(frames[i+1])-premul(frames[i])).mean()) for i in range(len(frames)-1)]
    original=[float(np.abs(premul(np.uint8(keys[(i+1)%count]*255))-premul(np.uint8(keys[i]*255))).mean()) for i in range(count if row in LOOPS else count-1)]
    metadata['actions'].append({'name':name,'keyframes':count,'steps':steps,'frames':len(frames),'loop':row in LOOPS,'maxFrameDifference':round(max(changes),4),'originalMaxDifference':round(max(original),4)})
    print(name,len(frames),'frames; max change',round(max(changes),3),'vs',round(max(original),3),flush=True)
(out/'manifest.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Total frames:',sum(a['frames'] for a in metadata['actions']),flush=True)
