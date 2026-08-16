(()=>{
  const VERSION='v23.01.19';
  let latestCup=null;
  let selectedMode='best';

  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const tacticText=t=>({0:'Normal',1:'Pres',2:'Kontra',3:'Ortadan Hücum',4:'Kanatlardan Hücum',7:'Yaratıcı'})[Number(t)]||`Taktik ${t??'?'}`;
  const typeText=t=>{const n=Number(t);if([2,3,7,9,11].includes(n))return'Kupa maçı';if([4,5,8,12,80,101,103,105,106].includes(n))return'Hazırlık maçı';return'Lig maçı'};
  const ratingsOf=(mode)=>mode==='cup' ? (latestCup?.record?.teamData?.ratings||{}) : (currentView?.ownRatings||{});
  const playersOf=(mode)=>mode==='cup' ? (latestCup?.record?.players||[]).map(p=>({name:p.name,role:p.role,roleKey:p.roleKey,rating:p.rating,behaviour:p.behaviour,form:'-',stamina:'-'})) : (currentView?.ownLineup?.players||[]);

  function setVersion(){
    document.querySelectorAll('.app-version').forEach(e=>e.textContent=VERSION);
    const f=document.querySelector('footer');if(f)f.textContent=f.textContent.replace(/v23\.01\.\d+/g,VERSION);
  }
  function ensureControls(){
    const panel=document.querySelector('.our-panel'); if(!panel)return;
    const head=panel.querySelector('.panel-head'); if(!head)return;
    if(document.getElementById('ownLineupMode'))return;
    const wrap=document.createElement('div');
    wrap.id='ownLineupMode';wrap.className='own-lineup-mode';
    wrap.innerHTML=`<div class="own-mode-label">ANALİZ KADROSU</div><div class="own-mode-buttons"><button type="button" data-mode="best" class="active">En iyi 11</button><button type="button" data-mode="cup">Son kupa 11</button></div><div id="ownModeSource" class="own-mode-source">Lig analizi / en iyi 11</div>`;
    head.appendChild(wrap);
    wrap.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>{selectedMode=b.dataset.mode;applyOwnMode();}));
  }
  function applyOwnMode(){
    ensureControls();
    const cupReady=!!latestCup?.record;
    if(selectedMode==='cup'&&!cupReady){
      selectedMode='best';
      const s=document.getElementById('ownModeSource');if(s)s.textContent='Son kupa 11 henüz yüklenmedi.';
    }
    document.querySelectorAll('#ownLineupMode button').forEach(b=>b.classList.toggle('active',b.dataset.mode===selectedMode));
    const r=ratingsOf(selectedMode), players=playersOf(selectedMode);
    const formation=selectedMode==='cup'?(latestCup?.record?.formation||'—'):(currentView?.ownLineup?.formation||'—');
    const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(r);
    const pill=document.getElementById('ownFormation');if(pill)pill.textContent=formation;
    const title=document.getElementById('ownLineupTitle');if(title)title.textContent=selectedMode==='cup'?'Son kupa 11':'En iyi 11';
    if(typeof renderPitch==='function')renderPitch('#ownPitch',players,false);
    const source=document.getElementById('ownModeSource');
    if(source){
      if(selectedMode==='cup'){
        const f=latestCup?.record?.fixture||{};
        source.textContent=`Kupa: ${fmtDay(f.matchDate)} • ${typeText(f.matchType)} • ${tacticText(latestCup?.record?.teamData?.tacticType)} Lv.${latestCup?.record?.teamData?.tacticLevel??'—'}`;
      }else source.textContent='Lig analizi • HO Engine en iyi 11';
    }
    if(currentView){
      if(!currentView.__bestTactic)currentView.__bestTactic=currentView.tactic||{};
      if(selectedMode==='cup')currentView.tactic={tacticType:latestCup.record.teamData.tacticType??0,tacticLevel:latestCup.record.teamData.tacticLevel??0,tacticName:tacticText(latestCup.record.teamData.tacticType)};
      else currentView.tactic=currentView.__bestTactic;
    }
    if(currentView){
      const home=currentView.isHome?r:(currentView.opponentRatings||{});
      const away=currentView.isHome?(currentView.opponentRatings||{}):r;
      if(typeof ratingForm==='function'){ratingForm('#homeRatings','home',home);ratingForm('#awayRatings','away',away)}
    }
  }
  function patchRender(){
    if(typeof window.renderMatch!=='function')return false;
    const old=window.renderMatch;
    window.renderMatch=function(x){old(x);ensureControls();applyOwnMode();decorateOpponentSource();};
    return true;
  }
  function decorateOpponentSource(){
    if(typeof currentView==='undefined'||!currentView)return;
    const i=Number(currentView.selectedRecentIndex??0),m=currentView.recentMatches?.[i],bar=document.getElementById('recentSelection');
    if(!bar||!m?.fixture)return;
    const f=m.fixture;
    const type=typeText(f.matchType);
    bar.innerHTML=`<b>Simülasyon baz alınan rakip maçı:</b> ${fmtDate(f.matchDate)} • ${esc(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName)} <span>• ${type} • ${esc(m.opponent?.formation||'—')} • ${tacticText(m.opponent?.tacticType)} ${m.opponent?.tacticLevel?`Lv.${m.opponent.tacticLevel}`:''}</span>`;
  }
  async function loadCup(){
    try{const r=await fetch('/api/cup-lineup/latest');const x=await jsonResponse(r);if(!r.ok)throw new Error(x.message||'Kupa kadrosu alınamadı.');latestCup=x;ensureControls();applyOwnMode();}
    catch(e){const s=document.getElementById('ownModeSource');if(s)s.textContent='Son kupa 11 alınamadı; En iyi 11 kullanılabilir.';}
  }
  const start=()=>{
    setVersion();ensureControls();
    patchRender();
    loadCup();
    setTimeout(()=>{ensureControls();decorateOpponentSource();applyOwnMode();},250);
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  window.setOwnAnalysisMode=(m)=>{selectedMode=m==='cup'?'cup':'best';applyOwnMode();};
})();
