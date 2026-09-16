const {contextBridge, ipcRenderer} = require('electron');
contextBridge.exposeInMainWorld('petDesktop', {
  load:()=>ipcRenderer.invoke('pet:load'),
  save:text=>ipcRenderer.invoke('pet:save',text),
  move:dx=>ipcRenderer.invoke('pet:move',dx),
  hit:hit=>ipcRenderer.send('pet:hit',!!hit),
  drag:active=>ipcRenderer.send('pet:drag',!!active),
  menu:()=>ipcRenderer.send('pet:menu'),
  notify:message=>ipcRenderer.send('pet:notify',message),
  onCommand:callback=>{const listener=(_event,payload)=>callback(payload);ipcRenderer.on('pet:command',listener);return()=>ipcRenderer.removeListener('pet:command',listener);},
});
