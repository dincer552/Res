(()=>{
const V='20260823-league-auto-patch-01';
let fixtureDiag=null;
const fmt=ms=>ms<1000?`${Math.round(ms)} ms`:`${(ms/1000).toFixed(1)} sn`;
const originalFetch=window.fetch.bind(window);
window.fetch=async function(...args){
  const response=await originalFetch(...args);
  try{
    const url=String(args[0]?.url||args[0]||'');
    if(url.includes('/api/fixture-view/')){
      const clone=response.clone();
      clone.json().then(x=>{
        const t=x?.chppTrace;
        if(t&&Array.isArray(t.calls)){
          const calls=t.calls;
          const total=calls.reduce((s,c)=>s+Number(c.durationMs||0),0);
          const slow=calls.reduce((a,c)=>Number(c.durationMs||0)>Number(a?.durationMs||0)?c:a,null);
          fixtureDiag={callCount:Number(t.callCount||calls.length),totalMs:total,maxMs:Number(slow?.durationMs||0),slowFile:slow?.file||'',slowContext:slow?.context||''};
          paint();
        }
      }).catch(()=>{});
    }
  }catch{}
  return response;
};
function paint(){
  const box=document.querySelector('#leagueAutoLog');
  if(!box)return;
  box.querySelectorAll('.league-auto-log-row').forEach(row=>{
    const title=row.querySelector('.league-auto-log-title')?.textContent?.trim()||'';
    const meta=row.querySelector('.league-auto-log-meta');
    if(!meta)return;
    const text=meta.textContent||'';
    if(title==='TOPLAM SÜRE'){
      row.classList.remove('pending','info','retry','error');row.classList.add('ok');row.dataset.status='completed';
      const icon=row.querySelector('.league-auto-log-icon');if(icon)icon.textContent='✓';
    }else if(!row.dataset.step){
      row.classList.remove('pending');row.dataset.status=row.dataset.status==='running'?'info':row.dataset.status;
    }
    if(fixtureDiag&&row.dataset.step==='matchData'&&text.includes('status=completed')&&!text.includes('CHPP trace')){
      const extra=`CHPP trace: ${fixtureDiag.callCount} çağrı • toplam ${fmt(fixtureDiag.totalMs)} • en yavaş ${fmt(fixtureDiag.maxMs)}${fixtureDiag.slowFile?` • ${fixtureDiag.slowFile}`:''}`;
      meta.textContent=`${text} • ${extra}`;
    }
  });
}
const observer=new MutationObserver(paint);
function boot(){const box=document.querySelector('#leagueAutoLog');if(box)observer.observe(box,{childList:true,subtree:true,characterData:true});paint();}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(boot,50));else setTimeout(boot,50);
setInterval(paint,1000);
})();