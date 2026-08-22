(()=>{
  let selectedMode='best';

  function clearOwnLineup(){
    const pitch=document.getElementById('ownPitch');
    if(pitch){pitch.querySelectorAll('.player-node,.lineup-empty').forEach(n=>n.remove());}
    const strip=document.getElementById('ownRatingStrip');
    if(strip)strip.innerHTML='';
    const formation=document.getElementById('ownFormation');
    if(formation)formation.textContent='—';
    const title=document.getElementById('ownLineupTitle');
    if(title)title.textContent='Önerilen Kadro';
    const source=document.getElementById('ownModeSource');
    if(source)source.textContent='';
  }

  function ensureControls(){
    clearOwnLineup();
  }

  function patchRender(){
    if(typeof window.renderMatch!=='function')return false;
    const old=window.renderMatch;
    if(old.__ownLineupGuard)return true;
    const wrapped=function(x){
      old(x);
      clearOwnLineup();
      if(typeof currentView!=='undefined'&&currentView){
        currentView.ownLineup=null;
        currentView.__selectedFormationResult=null;
        currentView.__selectedFormationResults={best:null,cup:null};
      }
    };
    wrapped.__ownLineupGuard=true;
    window.renderMatch=wrapped;
    return true;
  }

  const start=()=>{patchRender();ensureControls();};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  window.setOwnAnalysisMode=mode=>{
    selectedMode=mode==='cup'?'cup':'best';
    clearOwnLineup();
  };
})();