(()=>{
const ROOT='/';
const originalFetch=window.fetch.bind(window);
const cache=new Map();
const key=u=>new URL(u,location.href).pathname+new URL(u,location.href).search;
const json=async u=>{const r=await originalFetch(u,{credentials:'same-origin'});const text=await r.text();if(!r.ok)throw new Error(`HTTP ${r.status}`);return JSON.parse(text)};
async function preload(){
  if(window.__ipwPreloadPromise)return window.__ipwPreloadPromise;
  window.__ipwPreloadPromise=(async()=>{
    const fixtures=await json(`${ROOT}api/fixtures`);
    cache.set(key('/api/fixtures'),fixtures);
    const list=(fixtures.fixtures||[]).filter(f=>f&&f.matchId);
    let done=0;
    const workers=Array.from({length:Math.min(4,Math.max(1,list.length))},async()=>{
      while(done<list.length){
        const i=done++; const f=list[i];
        try{const data=await json(`${ROOT}api/fixture-view/${f.matchId}?recentIndex=0`);cache.set(key(`/api/fixture-view/${f.matchId}?recentIndex=0`),data)}catch(e){console.warn('Kadro planı veri ön yükleme:',f.matchId,e)}
      }
    });
    await Promise.all(workers);
    window.__ipwPrefetchReady=true;
    window.__ipwPrefetchCache=cache;
    return {fixtures:fixtures.fixtures||[],cached:cache.size};
  })().catch(e=>{window.__ipwPrefetchError=e;throw e});
  return window.__ipwPreloadPromise;
}
window.__ipwPreload=preload;
window.fetch=async function(input,init){
  const u=key(typeof input==='string'?input:(input?.url||''));
  if(!init?.method||String(init.method).toUpperCase()==='GET'){
    const data=cache.get(u);
    if(data!==undefined)return new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}});
  }
  return originalFetch(input,init);
};
})();
