(()=>{
function guard(){
  const name=document.getElementById('ipwStepName');
  const next=document.getElementById('ipwNext');
  if(!name||!next)return;
  if(name.textContent==='Tamamlandı'&&!next.dataset.finishGuarded){
    const fresh=next.cloneNode(true);
    fresh.dataset.finishGuarded='1';
    fresh.textContent='Kapat';
    fresh.disabled=false;
    fresh.onclick=()=>window.closeIntegratedPlanningWizard?.();
    next.replaceWith(fresh);
  }
}
new MutationObserver(guard).observe(document.documentElement,{childList:true,subtree:true,characterData:true});
setInterval(guard,500);
})();
