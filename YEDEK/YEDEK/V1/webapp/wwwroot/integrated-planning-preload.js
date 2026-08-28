(()=>{
const ROOT='/';
const originalFetch=window.fetch.bind(window);
const cache=new Map();
const key=u=>new URL(u,location.href).pathname+new URL(u,location.href).search;
const json=async u=>{const r=await originalFetch(u,{credentials:'same-origin',cache:'no-store'});const text=await r.text();if(!r.ok)throw new Error(`HTTP ${r.status}`);if(!text.trim())throw new Error('Boş yanıt');return JSON.parse(text)};
async function preload(){
  if(window.__ipwPreloadPromise)return window.__ipwPreloadPromise;
  window.__ipwPreloadPromise=(async()=>{
    // Kritik yol üzerinde bütün fixture-view isteklerini bekletmiyoruz.
    // Fikstür listesi hafifçe alınır; ağır rakip geçmişleri wizard adımında gerektiğinde yüklenir.
    let fixtures;
    try{fixtures=await json(`${ROOT}api/fixtures`)}catch(e){
      console.warn('Kadro planı fikstür ön yükleme başarısız:',e);
      fixtures={fixtures:[]};
    }
    cache.set(key('/api/fixtures'),fixtures);
    window.__ipwPrefetchReady=true;
    window.__ipwPrefetchCache=cache;
    return {fixtures:fixtures.fixtures||[],cached:cache.size,mode:'light-preload'};
  })();
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
