import React, {useEffect, useRef, useState} from 'react';
import {loadImage} from './images.mjs';

export default function PhotoEditor({asset, onSave, onCancel}) {
  const canvas = useRef(null), painting=useRef(false), history=useRef([]);
  const [radius,setRadius]=useState(36),[undoCount,setUndoCount]=useState(0),[ready,setReady]=useState(false);
  useEffect(() => {
    let cancelled=false;
    loadImage(asset.data).then(image => {
      if(cancelled)return;
      const c=canvas.current;c.width=asset.width;c.height=asset.height;
      c.getContext('2d').drawImage(image,0,0);
      setReady(true);
    });
    return () => {cancelled=true;};
  }, [asset]);
  function erase(event) {
    if (!painting.current) return;
    const c=canvas.current,rect=c.getBoundingClientRect(),ctx=c.getContext('2d');
    const x=(event.clientX-rect.left)*c.width/rect.width,y=(event.clientY-rect.top)*c.height/rect.height;
    ctx.save();ctx.globalCompositeOperation='destination-out';ctx.beginPath();ctx.arc(x,y,radius,0,Math.PI*2);ctx.fill();ctx.restore();
  }
  function down(event) {
    if(!ready)return;
    const c=canvas.current,ctx=c.getContext('2d');
    history.current.push(ctx.getImageData(0,0,c.width,c.height));
    if(history.current.length>5)history.current.shift();
    setUndoCount(history.current.length);
    painting.current=true;c.setPointerCapture(event.pointerId);erase(event);
  }
  function undo() {
    if(history.current.length){canvas.current.getContext('2d').putImageData(history.current.pop(),0,0);setUndoCount(history.current.length);}
  }
  return <section className="photo-editor" aria-label="修整背景">
    <div className="editor-header"><div><h3>擦掉多余的背景</h3><p>拖动画笔擦除。最多可撤销 5 次。</p></div><button onClick={onCancel}>返回</button></div>
    <canvas ref={canvas} onPointerDown={down} onPointerMove={erase} onPointerUp={()=>painting.current=false} onPointerCancel={()=>painting.current=false} aria-label="背景擦除画布"/>
    <div className="editor-tools"><label>画笔大小<input type="range" min="8" max="100" value={radius} onChange={e=>setRadius(Number(e.target.value))}/></label>
      <button disabled={!undoCount} onClick={undo}>撤销</button>
      <button className="primary" disabled={!ready} onClick={()=>onSave({...asset,data:canvas.current.toDataURL('image/png')})}>保存修整</button></div>
  </section>;
}
