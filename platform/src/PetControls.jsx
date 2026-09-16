import React, {useEffect, useRef} from 'react';
import {LOOK_NAMES} from './accessories.mjs';
const ACTION_NAMES={idle:'坐着',walk:'走一走',stretch:'伸懒腰',sleep:'睡一会儿'};
function Close({onClick,disabled}) {return <button type="button" className="pet-menu-close" aria-label="关闭动作与造型" onClick={onClick} disabled={disabled}><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" aria-hidden="true"><path d="m6 6 12 12M6 18 18 6"/></svg></button>;}
export function PetMenu({action,available,appearance,busy,onAction,onLook,onCycleLook,onAdjust,onClose}) {
  const ref=useRef(null);
  useEffect(()=>{ref.current?.querySelector('.pet-quick button:not(:disabled)')?.focus({preventScroll:true});},[]);
  return <section ref={ref} className="pet-menu pet-controls" role="dialog" aria-label="动作与造型" aria-modal="false">
    <div className="pet-menu-heading"><strong>陪它玩一会儿</strong><Close onClick={onClose} disabled={busy}/></div>
    <div className="pet-quick"><button onClick={()=>onAction('cycle')} disabled={busy||available.length<2}>换个姿势</button><button onClick={onCycleLook} disabled={busy}>换个造型</button></div>
    <fieldset disabled={busy}><legend>选个动作</legend><div className="pet-action-options">{Object.entries(ACTION_NAMES).map(([id,name])=><button key={id} aria-pressed={action===id} disabled={!available.includes(id)} onClick={()=>onAction(id)}>{name}</button>)}</div></fieldset>
    <fieldset disabled={busy}><legend>戴点什么</legend><div className="pet-look-options">{Object.entries(LOOK_NAMES).map(([id,name])=><button key={id} aria-pressed={appearance.style===id} onClick={()=>onLook(id)}><span className={'look-swatch look-'+id} aria-hidden="true"/>{name}</button>)}</div></fieldset>
    <div className="pet-menu-foot">{appearance.style!=='none'?<button className="text-link" onClick={onAdjust} disabled={busy}>调整配饰位置</button>:<span>原照片保留，配饰随时可取下。</span>}{busy&&<span role="status">保存中…</span>}</div>
    {available.length<2&&<p className="pet-menu-note">目前只有坐姿，可在“动作”中补充图片。</p>}
  </section>;
}
export function AccessoryFit({appearance,action,busy,onChange,onSave,onCancel}) {
  const anchor=appearance.anchors[action];
  const fields=[['x','左右',0,100],['y','上下',0,100],['width','大小',5,60],['angle','角度',-90,90]];
  return <section className="pet-fit pet-controls" role="dialog" aria-label="调整配饰位置" aria-modal="false">
    <div className="pet-fit-heading"><strong>调整配饰 · {ACTION_NAMES[action]}</strong><span>只调整当前姿势</span></div>
    <div className="pet-fit-fields">{fields.map(([id,label,min,max])=>{
      const multiplier=id==='angle'?1:100;
      return <label key={id}>{label}<output>{Math.round(anchor[id]*multiplier)}{id==='angle'?'°':'%'}</output><input type="range" aria-label={'配饰'+label} min={min} max={max} step={id==='angle'?1:.5} value={anchor[id]*multiplier} disabled={busy} onChange={event=>onChange({...appearance,anchors:{...appearance.anchors,[action]:{...anchor,[id]:Number(event.target.value)/multiplier}}})}/></label>;
    })}</div>
    <div className="pet-fit-buttons"><button onClick={onCancel} disabled={busy}>取消</button><button className="primary" onClick={onSave} disabled={busy}>{busy?'保存中…':'完成调整'}</button></div>
  </section>;
}