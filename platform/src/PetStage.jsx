import React, {useEffect, useRef, useState} from 'react';
import {PetEngine} from '../shared/engine.mjs';
import {loadImage} from './images.mjs';

export const ACTION_NAMES = {idle:'原地陪伴',walk:'走一走',stretch:'伸懒腰',sleep:'睡一会儿'};
export default function PetStage({pet, command, onState, background='mist', desktop=false}) {
  const canvasRef = useRef(null);
  const engine = useRef(null);
  const images = useRef(new Map());
  const onStateRef = useRef(onState);
  const pointer = useRef(null);
  const [bubble, setBubble] = useState('');
  const [loadError, setLoadError] = useState('');
  const bubbleTimer = useRef(null);
  const position = useRef({x:0,y:0});
  const speak = message => {
    setBubble(message);
    clearTimeout(bubbleTimer.current);
    bubbleTimer.current = setTimeout(() => setBubble(''), 5000);
  };
  onStateRef.current = onState;
  useEffect(() => {
    if (!engine.current) {engine.current = new PetEngine(pet);engine.current.paused = window.matchMedia('(prefers-reduced-motion: reduce)').matches;}
    else engine.current.setPet(pet);
  }, [pet]);
  useEffect(() => {
    let cancelled = false;
    setLoadError('');
    Promise.all(pet.assets.map(async asset => [asset.id, await loadImage(asset.data)])).then(entries => {
      if (!cancelled) images.current = new Map(entries);
    }).catch(() => { if (!cancelled) setLoadError('有图片无法显示，请重新导入这张图片。'); });
    return () => {cancelled=true;};
  }, [pet.assets]);
  useEffect(() => {
    if (!command || !engine.current) return;
    const value = engine.current;
    if (command.action === 'pause') value.paused = !value.paused;
    else if (command.action === 'focus') {
      value.focusRemaining = value.focusRemaining === null ? 25 * 60 : null;
      speak(value.focusRemaining === null ? '已结束这次专注。' : '接下来 25 分钟，我在这里陪你。');
    } else if (command.action === 'wake') value.wake();
    else if (command.action === 'greet') speak('我在这里，慢慢来。');
    else if (command.action === 'home') { value.x = 0; position.current={x:0,y:0}; }
    else value.play(command.action);
  }, [command]);
  useEffect(() => {
    let raf, previous = performance.now(), lastNotify = 0, movementClock=0, pendingDx=0;
    const canvas = canvasRef.current;
    const context = canvas.getContext('2d', {willReadFrequently:desktop});
    let w=1,h=1;
    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      w=rect.width; h=rect.height;
      const ratio = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.round(w * ratio); canvas.height = Math.round(h * ratio);
      context.setTransform(ratio, 0, 0, ratio, 0, 0);
    };
    const observer = new ResizeObserver(resize);
    observer.observe(canvas);
    const frame = now => {
      const delta = (now - previous) / 1000;
      previous = now;
      const value = engine.current;
      if (value) {
        const petSize = Math.min(value.pet.settings.size * (desktop ? 1 : 1.36), h - 65, w * (desktop ? 0.97 : 0.68));
        const room = Math.max(0, w - petSize - 32);
        const state = value.tick(pointer.current ? 0 : delta, desktop ? 0 : room);
        if (desktop && !pointer.current && !state.paused && state.dx) {
          pendingDx += state.dx; movementClock += delta;
          if (movementClock > .045) {
            window.petDesktop?.move(pendingDx).then(direction => {if(direction) value.direction=direction;});
            pendingDx=0; movementClock=0;
          }
        } else {pendingDx=0;movementClock=0;}
        context.clearRect(0, 0, w, h);
        const image = images.current.get(state.assetId);
        if (image) {
          const cx = w / 2 + (desktop ? 0 : state.x) + position.current.x;
          const cy = h - 28 + position.current.y;
          context.save();
          context.translate(cx, cy);
          context.scale(state.action === 'walk' && state.direction === 1 ? -1 : 1, 1 + state.breath);
          context.drawImage(image, -petSize/2, -petSize, petSize, petSize);
          context.restore();
        }
        for (const message of state.events) {
          speak(message);
          if (desktop) window.petDesktop?.notify(message);
        }
        if (now - lastNotify > 200) {
          onStateRef.current?.({...state});
          lastNotify=now;
        }
      }
      raf=requestAnimationFrame(frame);
    };
    raf=requestAnimationFrame(frame);
    return () => {cancelAnimationFrame(raf);observer.disconnect();clearTimeout(bubbleTimer.current);};
  }, [desktop]);
  function point(event) {
    const box=canvasRef.current.getBoundingClientRect();
    return {x:event.clientX-box.left,y:event.clientY-box.top};
  }
  function hits(event) {
    const c=canvasRef.current,p=point(event),box=c.getBoundingClientRect();
    try {
      const x=Math.max(0,Math.min(c.width-1,Math.floor(p.x*c.width/box.width)));
      const y=Math.max(0,Math.min(c.height-1,Math.floor(p.y*c.height/box.height)));
      return c.getContext('2d').getImageData(x,y,1,1).data[3] > 24;
    } catch {return false;}
  }
  function down(event) {
    if (event.button !== 0 || !hits(event)) return;
    const p=point(event);
    pointer.current={...p,startX:event.clientX,startY:event.clientY,offset:{...position.current},moved:false};
    event.currentTarget.setPointerCapture(event.pointerId);
    if (desktop) window.petDesktop?.drag(true);
  }
  function move(event) {
    if (desktop) window.petDesktop?.hit(!!pointer.current || hits(event));
    if (!pointer.current) return;
    const dx=event.clientX-pointer.current.startX,dy=event.clientY-pointer.current.startY;
    if (Math.abs(dx)+Math.abs(dy)>5) pointer.current.moved=true;
    if (!desktop) {
      const c=canvasRef.current.getBoundingClientRect();
      position.current={
        x:Math.max(-c.width*.25,Math.min(c.width*.25,pointer.current.offset.x+dx)),
        y:Math.max(-c.height*.25,Math.min(0,pointer.current.offset.y+dy)),
      };
    }
  }
  function up(event) {
    if (!pointer.current) return;
    if (!pointer.current.moved) {
      if (engine.current.action === 'sleep') engine.current.wake();
      speak('摸摸收到了，今天也陪着你。');
    }
    pointer.current=null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    if (desktop) window.petDesktop?.drag(false);
  }
  return <div className={'pet-stage stage-'+background+(desktop?' desktop-stage':'')}>
    {bubble && <div className="pet-bubble" role="status">{bubble}</div>}
    {loadError && <p className="stage-error" role="alert">{loadError}</p>}
    <canvas ref={canvasRef} aria-label={pet.pet.name+'的动作预览，点击或拖动宠物'} role="img"
      onPointerDown={down} onPointerMove={move} onPointerUp={up} onPointerCancel={up}
      onPointerLeave={() => {if(desktop && !pointer.current) window.petDesktop?.hit(false);}}
      onContextMenu={event => {if(desktop){event.preventDefault();window.petDesktop?.menu();}}}/>
  </div>;
}
