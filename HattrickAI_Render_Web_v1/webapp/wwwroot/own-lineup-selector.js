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
    if(title)title.textContent='Kadro hesaplanmadı';
    const source=document.getElementById('ownModeSource');
    if(source)source.textContent='Önce Kadro Planını Hesapla';
  }

  function ensureControls(){
    const panel=document.querySelector('.our-panel');
    const head=panel?.querySelector('.panel-head');
    if(!head)return;
    if(!document.getElementById('ownLineupMode')){
      const wrap=document.createElement('div');
      wrap.id='ownLineupMode';
      wrap.className='own-lineup-mode';
      wrap.innerHTML='<div class="own-mode-label">KADRO TÜRÜ</div><div class="own-mode-buttons"><button type="button" data-mode="best">Lig Kadrosu</button><button type="button" data-mode="cup">Kupa Kadrosu</button></div><div id="ownModeSource" class="own-mode-source"></div>';
      head.appendChild(wrap);
      wrap.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>{
        selectedMode=b.dataset.mode==='cup'?'cup':'best';
        wrap.querySelectorAll('button').forEach(x=>x.classList.toggle('active',x.dataset.mode===selectedMode));
        clearOwnLineup();
      }));
    }
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
    document.querySelectorAll('#ownLineupMode button').forEach(b=>b.classList.toggle('active',b.dataset.mode===selectedMode));
    clearOwnLineup();
  };
})();