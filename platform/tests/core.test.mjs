import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {parsePackage,validatePackage,serializePackage,newPackage,pruneAssets} from '../shared/pet-package.mjs';
import {PetEngine,clampPosition} from '../shared/engine.mjs';
const folder=new URL('../public/demo/',import.meta.url);
function demo(){
 const pet=JSON.parse(readFileSync(new URL('pet.json',folder),'utf8'));
 for(const asset of pet.assets){asset.data='data:image/png;base64,'+readFileSync(new URL(asset.file,folder)).toString('base64');delete asset.file;}
 return validatePackage(pet);
}
const source=demo();
function copy(){return structuredClone(source);}
test('package round-trip preserves photo pixels, actions and settings',()=>{
 const pet=copy();pet.pet.name='奶糖';pet.settings.reminder.enabled=true;
 assert.deepEqual(parsePackage(serializePackage(pet)),pet);
});
test('new photo starts with idle only and cannot borrow demo poses',()=>{
 const pet=newPackage('我的猫',source.assets[0]);assert.deepEqual(Object.keys(pet.actions),['idle']);
 assert.equal(new PetEngine(pet).play('walk'),false);
});
for(const [name,mutate] of [
 ['remote image URL',p=>p.assets[0].data='https://example.com/cat.png'],
 ['script image',p=>p.assets[0].data='data:image/svg+xml;base64,PHN2Zy8+'],
 ['duplicate IDs',p=>p.assets[1].id=p.assets[0].id],
 ['unresolved frame',p=>p.actions.idle.frames=['missing']],
 ['single-photo walking',p=>p.actions.walk.frames=['cat']],
 ['mismatched dimensions',p=>p.assets[0].width=1200],
 ['oversized dimension',p=>p.assets[0].width=100000],
 ['unknown version',p=>p.version=2],
 ['invalid reminder',p=>p.settings.reminder.minutes=-1],
 ['empty name',p=>p.pet.name=' '],
 ['invalid fps',p=>p.actions.walk.fps=0],
])test('rejects '+name,()=>{const p=copy();mutate(p);assert.throws(()=>validatePackage(p));});
test('invalid JSON has a recoverable error',()=>assert.throws(()=>parsePackage('oops'),/无法读取/));
test('extra untrusted script fields are discarded',()=>{
 const p=copy();p.script='alert(1)';p.pet.html='<script>';p.actions.code={frames:['cat'],fps:1};
 const value=validatePackage(p);assert.equal(value.script,undefined);assert.equal(value.pet.html,undefined);assert.equal(value.actions.code,undefined);
});
test('removed action does not leave orphaned photos in package',()=>{
 const pet=copy();delete pet.actions.walk;const clean=pruneAssets(pet);assert.equal(clean.assets.length,3);
});
test('automatic rotation includes every available action',()=>{
 const e=new PetEngine(source,()=>.4),visited=new Set();
 for(let i=0;i<2400;i++)visited.add(e.tick(.1,300).action);
 assert.deepEqual([...visited].sort(),['idle','sleep','stretch','walk']);
});
test('manual sleep waits until explicitly woken',()=>{
 const e=new PetEngine(source);e.play('sleep');for(let i=0;i<3600;i++)e.tick(.1);assert.equal(e.action,'sleep');e.wake();assert.equal(e.action,'stretch');
});
test('automatic sleep eventually wakes',()=>{
 const e=new PetEngine(source);e.play('sleep',false);for(let i=0;i<330;i++)e.tick(.1);assert.equal(e.action,'idle');
});
test('pause freezes action clocks, position and focus',()=>{
 const e=new PetEngine(source);e.play('walk');e.focusRemaining=1500;e.tick(.1);const before=[e.elapsed,e.x,e.focusRemaining];e.paused=true;
 for(let i=0;i<100;i++)e.tick(.1);assert.deepEqual([e.elapsed,e.x,e.focusRemaining],before);
});
test('disabled auto-play remains idle after manual clip',()=>{
 const p=copy();p.settings.autoPlay=false;const e=new PetEngine(p);e.play('walk');for(let i=0;i<900;i++)e.tick(.1);assert.equal(e.action,'idle');
});
test('walk stays inside preview and reverses at edge',()=>{
 const e=new PetEngine(source);e.play('walk');const seen=new Set();for(let i=0;i<50;i++){const s=e.tick(.1,40);seen.add(s.direction);assert.ok(Math.abs(s.x)<=20);}
 assert.equal(seen.size,2);
});
test('changing pets removes unavailable active animation',()=>{
 const e=new PetEngine(source);e.play('walk');e.setPet(newPackage('新猫',source.assets[0]));assert.equal(e.action,'idle');
});
test('reminders and focus completion trigger once',()=>{
 const p=copy();p.settings.reminder.enabled=true;p.settings.reminder.minutes=5;const e=new PetEngine(p);e.focusRemaining=.2;let messages=[];
 for(let i=0;i<3002;i++)messages.push(...e.tick(.1).events);
 assert.equal(messages.filter(m=>m.includes('专注完成')).length,1);
 assert.equal(messages.filter(m=>m===p.settings.reminder.message).length,1);
});
test('multi-monitor clamp preserves negative desktop coordinates',()=>{
 assert.deepEqual(clampPosition(-2400,-30,400,420,{x:-1920,y:0,width:1920,height:1040}),{x:-1920,y:0});
 assert.deepEqual(clampPosition(9000,9000,400,420,{x:0,y:0,width:1920,height:1040}),{x:1520,y:620});
});

test('a delayed frame keeps real focus time without teleporting the pet',()=>{
 const e=new PetEngine(source);e.play('walk');e.focusRemaining=60;
 const result=e.tick(20,400);assert.equal(result.focusRemaining,40);assert.ok(Math.abs(result.dx)<6);
});


test('legacy pet packages gain removable default accessories without changing photos',()=>{
 const pet=copy();delete pet.settings.appearance;const parsed=validatePackage(pet);
 assert.equal(parsed.settings.appearance.style,'none');assert.deepEqual(parsed.assets,pet.assets);
});
test('accessory choice and per-pose position survive export and import',()=>{
 const pet=copy();pet.settings.appearance.style='bow';pet.settings.appearance.anchors.sleep={x:.7,y:.8,width:.18,angle:-15};
 assert.deepEqual(parsePackage(serializePackage(pet)).settings.appearance,pet.settings.appearance);
});
for(const [name,mutate] of [
 ['unrecognized accessory',p=>p.settings.appearance.style='https://example.com/accessory.svg'],
 ['nonfinite placement',p=>p.settings.appearance.anchors.idle.x=NaN],
 ['off-canvas placement',p=>p.settings.appearance.anchors.idle.y=3],
 ['oversized accessory',p=>p.settings.appearance.anchors.walk.width=10],
])test('rejects '+name,()=>{const p=copy();mutate(p);assert.throws(()=>validatePackage(p));});
test('quick pose cycling visits only available poses and wraps around',()=>{
 const p=copy();delete p.actions.walk;const e=new PetEngine(p);
 e.cycle();assert.equal(e.action,'stretch');e.cycle();assert.equal(e.action,'sleep');e.cycle();assert.equal(e.action,'idle');
 const one=new PetEngine(newPackage('新猫',source.assets[0]));assert.equal(one.cycle(),false);assert.equal(one.action,'idle');
});
test('using controls holds the pose and movement without losing focus time',()=>{
 const e=new PetEngine(source);e.play('walk');e.tick(.1);e.focusRemaining=100;
 const before={elapsed:e.elapsed,x:e.x};const frame=e.tick(15,300,true);
 assert.equal(frame.focusRemaining,85);assert.equal(frame.dx,0);assert.equal(e.x,before.x);assert.equal(e.elapsed,before.elapsed);
});
