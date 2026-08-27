(()=>{
  // Compatibility shim: selected opponent match is rendered by ui-fixes.js.
  // This file must not overwrite that data with the old generic "Son maç" footer.
  const V='20260827-opponent-match-footer-06';
  function patch(){
    const box=document.querySelector('#selectedOpponentMatch');
    if(!box)return;
    box.classList.remove('opponent-match-footer');
    box.querySelectorAll('.opponent-match-footer-label,.opponent-match-footer-match').forEach(el=>el.remove());
    const stale=box.textContent.trim();
    if(stale==='Son maç') box.textContent='';
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',patch);else patch();
})();
