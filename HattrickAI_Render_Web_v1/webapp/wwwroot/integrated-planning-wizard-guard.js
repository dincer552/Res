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
function fixScroll(){
  const body=document.getElementById('ipwBody');
  const shell=document.querySelector('.ipw-shell');
  if(body){body.style.overflowY='auto';body.style.overflowX='hidden';body.style.webkitOverflowScrolling='touch';body.style.touchAction='pan-y';}
  if(shell)shell.style.maxHeight='94vh';
}
function guard(){
  fixScroll();
  const name=document.getElementById('ipwStepName'),next=document.getElementById('ipwNext');
  if(!name||!next)return;
  if(name.textContent==='Tamamlandı'&&!next.dataset.finishGuarded){
    const fresh=next.cloneNode(true);fresh.dataset.finishGuarded='1';fresh.textContent='Kapat';fresh.disabled=false;fresh.onclick=()=>window.closeIntegratedPlanningWizard?.();next.replaceWith(fresh);
  }
}
new MutationObserver(guard).observe(document.documentElement,{childList:true,subtree:true,characterData:true});
setInterval(guard,300);
})();
