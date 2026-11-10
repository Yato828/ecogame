// Exercises the real jslib queue/callbacks with a fake FS. This is not an IndexedDB/Unity Web test.
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict');
const source=fs.readFileSync('Assets/_WATTSHIFT/Plugins/WebGL/WattshiftPersistence.jslib','utf8');
function harness(throwFs=false){
 const callbacks=[],messages=[],events={},lib={};
 const ctx={LibraryManager:{library:lib},mergeInto:Object.assign,UTF8ToString:x=>x,
 FS:{syncfs:(populate,done)=>{assert.equal(populate,false);if(throwFs)throw Error('quota');callbacks.push(done);}},
 SendMessage:(...args)=>messages.push(args),document:{hidden:true,addEventListener:(n,f)=>events[n]=f},window:{addEventListener:(n,f)=>events[n]=f}};
 vm.createContext(ctx);vm.runInContext(source,ctx);ctx.WattshiftPersistenceState=lib.$WattshiftPersistenceState;
 return {lib,ctx,callbacks,messages,events};
}
{
 const h=harness();h.lib.Wattshift_SyncPersistentData('receiver',1);h.lib.Wattshift_SyncPersistentData('receiver',2);
 assert.equal(h.callbacks.length,1);h.callbacks[0](null);assert.deepEqual(h.messages[0],['receiver','OnStorageSync','1:ok']);
 assert.equal(h.callbacks.length,2);h.callbacks[1](Error('QuotaExceededError'));assert.deepEqual(h.messages[1],['receiver','OnStorageSync','2:error']);
 assert.equal(h.ctx.WattshiftPersistenceState.busy,false);
}
{
 const h=harness(true);h.lib.Wattshift_SyncPersistentData('receiver',9);assert.deepEqual(h.messages,[['receiver','OnStorageSync','9:error']]);assert.equal(h.ctx.WattshiftPersistenceState.busy,false);
}
{
 const h=harness();h.lib.Wattshift_RegisterPageLifecycle('receiver');h.lib.Wattshift_RegisterPageLifecycle('other');
 h.events.visibilitychange();h.events.pagehide();assert.equal(h.messages.length,2);assert.equal(h.messages[0][0],'receiver');assert.equal(h.messages[1][1],'FlushFromPage');
}
{
 const h=harness();const original=h.ctx.SendMessage;
 h.ctx.SendMessage=(...args)=>{original(...args);if(args[2]==='4:ok')h.lib.Wattshift_SyncPersistentData('receiver',5);};
 h.lib.Wattshift_SyncPersistentData('receiver',4);h.callbacks[0](null);assert.equal(h.callbacks.length,2);h.callbacks[1](null);
 assert.equal(h.messages[1][2],'5:ok');assert.equal(h.ctx.WattshiftPersistenceState.busy,false);
}
console.log('PASS: 4 bridge protocol cases (mock FS; browser/IDBFS NOT TESTED)');
