// Small geometric accessories are drawn above the photo; original pixels stay intact.
export const LOOK_NAMES = {none:'原样', scarf:'红围巾', bow:'蓝领结'};
function shape(ctx, data, color) {ctx.fillStyle=color;ctx.fill(new Path2D(data));}
export function drawAccessory(ctx, appearance, action, size) {
  if (!appearance || appearance.style === 'none') return;
  const anchor=appearance.anchors[action];
  ctx.save();
  ctx.translate((anchor.x-.5)*size,(anchor.y-1)*size);
  ctx.rotate(anchor.angle*Math.PI/180);
  ctx.scale(anchor.width*size,anchor.width*size);
  if(appearance.style==='scarf') {
    shape(ctx,'M-.48 -.15 Q0 .03 .48 -.15 L.44 .08 Q0 .24 -.44 .08 Z','#b64043');
    shape(ctx,'M.04 .09 L.24 .06 L.33 .69 L.08 .72 L.12 .28 Z','#ac3239');
    shape(ctx,'M.22 .08 L.36 .03 L.55 .49 L.32 .57 L.27 .3 Z','#cd5451');
    shape(ctx,'M-.47 -.15 Q0 .03 .48 -.15 L.47 -.06 Q0 .13 -.46 -.06 Z','#d56360');
    shape(ctx,'M.04 .03 Q.18 -.04 .32 .02 L.3 .21 Q.16 .28 .06 .16 Z','#c4474a');
    ctx.strokeStyle='#e49a88';ctx.lineWidth=.012;
    for(let x=.11;x<.32;x+=.045){ctx.beginPath();ctx.moveTo(x,.66);ctx.lineTo(x+.002,.72);ctx.stroke();}
  } else if(appearance.style==='bow') {
    shape(ctx,'M-.03 -.06 Q-.30 -.31 -.49 -.24 Q-.57 0 -.46 .23 Q-.24 .24 -.03 .07 Z','#315f78');
    shape(ctx,'M.03 -.06 Q.30 -.31 .49 -.24 Q.57 0 .46 .23 Q.24 .24 .03 .07 Z','#477d98');
    shape(ctx,'M-.1 .07 L-.17 .42 L-.01 .36 L.1 .4 L.08 .07 Z','#315f78');
    shape(ctx,'M-.45 -.19 L-.09 -.03 L-.35 -.04 Z','#6093a9');
    shape(ctx,'M.45 -.19 L.09 -.03 L.35 -.04 Z','#7babbc');
    shape(ctx,'M-.085 -.115 Q0 -.16 .085 -.115 L.09 .13 Q0 .18 -.09 .13 Z','#264d63');
  }
  ctx.restore();
}