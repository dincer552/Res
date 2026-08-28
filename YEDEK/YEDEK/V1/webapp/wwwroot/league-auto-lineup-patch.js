(()=>{
const V='20260823-league-auto-patch-03';
let fixtureDiag=null;
let chppReady=false;
let chppWaiters=[];
const fmt=ms=>ms<1000?`${Math.round(ms)} ms`:`${(ms/1000).toFixed(1)} sn`;
const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
const originalFetch=window.fetch.bind(window);
function setConnectionUi(connected){
  chppReady=!!connected;
  const panel=document.getElementById('leagueAutoPanel');
  const log=document.getElementById('leagueAutoLog');
  const status=document.getElementById('leagueAutoStatus');
  const progress=panel?.querySelector('.league-auto-progress');
  if(!chppReady){
    if(log)log.style.display='none';
    if(status)status.style.display='none';
    if(progress)progress.style.display='none';
    if(panel)panel.classList.remove('error','ready');
  }else{
    if(log)log.style.display='';
    if(status)status.style.display='';
    if(progress)progress.style.display='';
  }
}
function waitForChpp(){
  if(chppReady)return Promise.resolve();
  return new Promise(resolve=>chppWaiters.push(resolve));
}
async function pollConnection(){
  try{
    const r=await originalFetch(`/api/status?trace=${Date.now()}`,{cache:'no-store'});
    const x=await r.json();
    const connected=!!x?.connected;
    if(connected&&!chppReady){
      chppReady=true;
      const waiters=chppWaiters;chppWaiters=[];waiters.forEach(resolve=>resolve());
    }
    setConnectionUi(connected);
  }catch{setConnectionUi(false)}
}
window.fetch=async function(...args){
  const url=String(args[0]?.url||args[0]||'');
  if(!chppReady&&(url.includes('/api/fixtures')||url.includes('/api/fixture-view/')||url.includes('/api/simulate'))){
    await waitForChpp();
  }
  const response=await originalFetch(...args);
  try{
    if(url.includes('/api/fixture-view/')){
      const clone=response.clone();
      clone.json().then(x=>{
        const t=x?.chppTrace;
        if(t&&Array.isArray(t.calls)){
          const calls=t.calls;
          const total=calls.reduce((s,c)=>s+Number(c.durationMs||0),0);
          const network=calls.reduce((s,c)=>s+Number(c.networkMs||0),0);
          const cache=calls.reduce((s,c)=>s+Number(c.cacheLookupMs||0)+Number(c.cacheWriteMs||0),0);
          const slow=calls.reduce((a,c)=>Number(c.durationMs||0)>Number(a?.durationMs||0)?c:a,null);
          fixtureDiag={calls,callCount:Number(t.callCount||calls.length),totalMs:total,networkMs:network,cacheMs:cache,maxMs:Number(slow?.durationMs||0),slowFile:slow?.file||'',slowContext:slow?.context||''};
          paint();
        }
      }).catch(()=>{});
    }
  }catch{}
  return response;
};
function ensureTraceBox(anchor){
  let box=document.getElementById('leagueChppTraceDetails');
  if(box)return box;
  box=document.createElement('div');box.id='leagueChppTraceDetails';
  box.style.cssText='margin:8px 0 4px;padding:8px;border:1px solid rgba(100,180,210,.25);border-radius:10px;background:rgba(0,20,30,.35);font-size:11px;line-height:1.35';
  const head=document.createElement('div');head.id='leagueChppTraceHead';head.style.cssText='font-weight:700;margin-bottom:6px';box.appendChild(head);
  const list=document.createElement('div');list.id='leagueChppTraceList';box.appendChild(list);
  if(anchor?.parentNode)anchor.parentNode.insertBefore(box,anchor.nextSibling);else document.querySelector('#leagueAutoLog')?.appendChild(box);
  return box;
}
function paintTrace(){
  if(!fixtureDiag)return;
  const row=[...document.querySelectorAll('#leagueAutoLog .league-auto-log-row')].find(r=>r.dataset.step==='matchData');
  if(!row)return;
  const box=ensureTraceBox(row);
  const head=box.querySelector('#leagueChppTraceHead');
  head.textContent=`CHPP DETAY • ${fixtureDiag.callCount} çağrı • toplam ${fmt(fixtureDiag.totalMs)} • network ${fmt(fixtureDiag.networkMs)} • cache ${fmt(fixtureDiag.cacheMs)} • en yavaş ${fmt(fixtureDiag.maxMs)}`;
  const list=box.querySelector('#leagueChppTraceList');list.innerHTML='';
  fixtureDiag.calls.forEach(c=>{
    const ok=c.success!==false;
    const source=c.cacheHit?'CACHE':'NETWORK';
    const phase=[];if(c.cacheLookupMs!=null)phase.push(`cache=${fmt(c.cacheLookupMs)}`);if(c.networkMs!=null)phase.push(`net=${fmt(c.networkMs)}`);if(c.cacheWriteMs!=null)phase.push(`write=${fmt(c.cacheWriteMs)}`);
    const title=`#${c.sequence} ${ok?'✓':'✕'} ${c.file||'?'}`;
    const meta=[source,c.context||'fixture-view',`total=${fmt(Number(c.durationMs||0))}`,`HTTP ${c.httpStatus??'?'}`,...phase];
    const line=document.createElement('div');line.style.cssText=`padding:5px 0;border-top:1px solid rgba(100,180,210,.12);color:${ok?'inherit':'#ff8f8f'}`;
    line.innerHTML=`<div><b>${esc(title)}</b> • ${esc(meta.join(' • '))}</div><div style="opacity:.72;word-break:break-word">${esc(c.query||'')}${c.error?` • HATA: ${esc(c.error)}`:''}</div>`;
    list.appendChild(line);
  });
}
function paint(){
  const box=document.querySelector('#leagueAutoLog');
  setConnectionUi(chppReady);
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
    if(fixtureDiag&&row.dataset.step==='matchData'&&text.includes('status=completed')){
      const extra=`CHPP: ${fixtureDiag.callCount} çağrı • ${fmt(fixtureDiag.totalMs)} • net ${fmt(fixtureDiag.networkMs)} • cache ${fmt(fixtureDiag.cacheMs)} • yavaş ${fmt(fixtureDiag.maxMs)}${fixtureDiag.slowFile?` • ${fixtureDiag.slowFile}`:''}`;
      if(!text.includes('CHPP:'))meta.textContent=`${text} • ${extra}`;
    }
  });
  paintTrace();
}
const observer=new MutationObserver(paint);
function boot(){
  setConnectionUi(false);
  const box=document.querySelector('#leagueAutoLog');
  if(box)observer.observe(box,{childList:true,subtree:true,characterData:true});
  paint();
  pollConnection();
  setInterval(pollConnection,1000);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(boot,50));else setTimeout(boot,50);
setInterval(paint,1000);
})();