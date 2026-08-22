(()=>{
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const originalFetch=window.fetch.bind(window);
function fallbackFixtureView(){
  const cv=window.currentView;
  if(!cv)return null;
  return {fixture:cv.fixture||{},opponentTeam:cv.opponentTeam||{},opponentRatings:cv.opponentRatings||cv.opponent?.ratings||{},tactic:cv.tactic||{},isHome:!!cv.isHome,formation:cv.formation||'3-5-2',recentMatches:cv.recentMatches||[]};
}
window.fetch=async function(input,init){
  const url=typeof input==='string'?input:(input?.url||'');
  try{return await originalFetch(input,init)}catch(err){
    if(String(url).includes('/api/fixture-view/')){
      for(let i=0;i<2;i++){await sleep(250*(i+1));try{return await originalFetch(input,init)}catch(_){}}
      const fallback=fallbackFixtureView();
      if(fallback)return new Response(JSON.stringify(fallback),{status:200,headers:{'Content-Type':'application/json'}});
    }
    throw err;
  }
};
function removeDuplicateTrainingErrors(){
  const body=document.getElementById('ipwBody');
  if(!body)return;
  [...body.querySelectorAll('*')].forEach(el=>{
    if(el.children.length===0 && el.textContent?.trim()==='Antrenmanı onayla.')el.remove();
  });
}
function syncTrainingButton(){
  const body=document.getElementById('ipwBody');
  const name=document.getElementById('ipwStepName');
  const next=document.getElementById('ipwNext');
  if(!body||!name||!next)return;
  removeDuplicateTrainingErrors();
  const isTrainingStep=name.textContent?.trim()==='Antrenmanı onayla';
  if(isTrainingStep){
    const toggle=body.querySelector('.ipw-check');
    const confirmed=!!toggle?.classList.contains('on');
    next.disabled=!confirmed;
    next.title=confirmed?'Antrenman onaylandı.':'Önce antrenmanı onayla.';
  }
}
function fixScroll(){
  const body=document.getElementById('ipwBody');
  const shell=document.querySelector('.ipw-shell');
  const backdrop=document.getElementById('ipwBackdrop');
  const host=document.getElementById('integratedPlanningInlineHost');
  if(body){body.style.overflowY='auto';body.style.overflowX='hidden';body.style.webkitOverflowScrolling='touch';body.style.touchAction='pan-y';body.style.height='auto';body.style.minHeight='0';}
  if(shell){shell.style.maxHeight='none';shell.style.height='auto';shell.style.minHeight='0';}
  if(host&&backdrop){backdrop.style.position='static';backdrop.style.overflow='visible';backdrop.style.touchAction='auto';}
}
function guard(){
  fixScroll();
  syncTrainingButton();
  const name=document.getElementById('ipwStepName'),next=document.getElementById('ipwNext');
  if(!name||!next)return;
  if(name.textContent==='Tamamlandı'&&!next.dataset.finishGuarded){
    const fresh=next.cloneNode(true);fresh.dataset.finishGuarded='1';fresh.textContent='Kapat';fresh.disabled=false;fresh.onclick=()=>window.closeIntegratedPlanningWizard?.();next.replaceWith(fresh);
  }
}
document.addEventListener('click',e=>{if(e.target.closest?.('.ipw-check'))setTimeout(syncTrainingButton,0)},true);
new MutationObserver(guard).observe(document.documentElement,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['class','style']});
setInterval(guard,300);
})();
