(()=>{
  let latestCup=null;
  let selectedMode='best';
  let selectedFormation=null;
  let ownTeamPromise=null;
  let formationBusy=false;

  const FORMATIONS=['4-4-2','4-3-3','3-5-2','4-5-1','5-4-1','5-3-2','3-4-3'];
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const tacticText=t=>({0:'Normal',1:'Pres',2:'Kontra',3:'Ortadan Hücum',4:'Kanatlardan Hücum',7:'Yaratıcı'})[Number(t)]||`Taktik ${t??'?'}`;
  const typeText=t=>{const n=Number(t);if([2,3,7,9,11].includes(n))return'Kupa maçı';if([4,5,8,12,80,101,103,105,106].includes(n))return'Hazırlık maçı';return'Lig maçı'};
  const roleText=k=>({Goalkeeper:'KL',LeftDefender:'SLB',CentralDefender:'STP',RightDefender:'SGB',LeftMidfielder:'OS',CentralMidfielder:'OM',RightMidfielder:'OS',LeftWinger:'K',RightWinger:'K',LeftForward:'SF',CentralForward:'SF',RightForward:'SF'})[k]||'';
  const ratingsOf=mode=>mode==='cup'?(latestCup?.record?.teamData?.ratings||{}):(currentView?.ownRatings||{});
  const playersOf=mode=>mode==='cup'?(latestCup?.record?.players||[]).map(p=>({name:p.name,role:p.role,roleKey:p.roleKey,rating:p.rating,behaviour:p.behaviour,form:'-',stamina:'-'})):(currentView?.ownLineup?.players||[]);

  function ensureControls(){
    const panel=document.querySelector('.our-panel'); if(!panel)return;
    const head=panel.querySelector('.panel-head'); if(!head)return;
    if(!document.getElementById('ownLineupMode')){
      const wrap=document.createElement('div');wrap.id='ownLineupMode';wrap.className='own-lineup-mode';
      wrap.innerHTML=`<div class="own-mode-label">ANALİZ KADROSU</div><div class="own-mode-buttons"><button type="button" data-mode="best" class="active">En iyi 11</button><button type="button" data-mode="cup">Son kupa 11</button></div><div id="ownModeSource" class="own-mode-source">Lig analizi / en iyi 11</div>`;
      head.appendChild(wrap);wrap.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>{selectedMode=b.dataset.mode;applyOwnMode();}));
    }
    if(!document.getElementById('ownFormationWrap')){
      const old=document.getElementById('ownFormation');if(!old)return;
      const current=old.textContent?.trim();if(!selectedFormation&&FORMATIONS.includes(current))selectedFormation=current;
      const wrap=document.createElement('div');wrap.id='ownFormationWrap';wrap.className='own-formation-wrap';
      wrap.innerHTML=`<button id="ownFormation" class="own-formation-button" type="button" aria-haspopup="true" aria-expanded="false">${esc(selectedFormation||current||'—')}</button><div id="ownFormationMenu" class="own-formation-menu">${FORMATIONS.map(f=>`<button type="button" class="own-formation-option" data-formation="${f}"><span>${f}</span><span class="own-formation-check"></span></button>`).join('')}</div><div id="ownFormationStatus" class="own-formation-status"></div>`;
      old.replaceWith(wrap);wrap.querySelector('#ownFormation').addEventListener('click',toggleFormationMenu);wrap.querySelectorAll('.own-formation-option').forEach(b=>b.addEventListener('click',()=>selectFormation(b.dataset.formation)));
      document.addEventListener('click',e=>{if(!wrap.contains(e.target))closeFormationMenu();});updateFormationChecks();
    }
  }

  function toggleFormationMenu(){const menu=document.getElementById('ownFormationMenu'),btn=document.getElementById('ownFormation');if(!menu||!btn)return;const open=!menu.classList.contains('open');menu.classList.toggle('open',open);btn.setAttribute('aria-expanded',String(open));}
  function closeFormationMenu(){const menu=document.getElementById('ownFormationMenu'),btn=document.getElementById('ownFormation');if(!menu||!btn)return;menu.classList.remove('open');btn.setAttribute('aria-expanded','false');}
  function updateFormationChecks(){document.querySelectorAll('.own-formation-option').forEach(b=>{const selected=b.dataset.formation===selectedFormation;b.classList.toggle('selected',selected);const check=b.querySelector('.own-formation-check');if(check)check.textContent=selected?'✓':'';});const btn=document.getElementById('ownFormation');if(btn)btn.textContent=selectedFormation||'—';}

  async function getOwnTeam(){
    if(ownTeamPromise)return ownTeamPromise;
    ownTeamPromise=fetch('/api/team').then(jsonResponse).then(x=>{if(!x?.players?.length)throw new Error('Takım kadrosu alınamadı.');return x;}).catch(e=>{ownTeamPromise=null;throw e;});
    return ownTeamPromise;
  }

  async function selectFormation(formation){if(!FORMATIONS.includes(formation)||formationBusy)return;selectedFormation=formation;selectedMode='best';updateFormationChecks();closeFormationMenu();await runFormationAnalysis();}

  async function runFormationAnalysis(){
    if(!selectedFormation||typeof currentView==='undefined'||!currentView)return;
    formationBusy=true;const btn=document.getElementById('ownFormation'),status=document.getElementById('ownFormationStatus');
    if(btn){btn.classList.add('loading');btn.textContent='…';}if(status)status.textContent=`${selectedFormation} için HO Engine düşünüyor…`;
    try{
      const team=await getOwnTeam();
      const opponent={teamName:currentView.opponentTeam?.teamName||currentView.opponentTeam?.TeamName||'Rakip',ratings:currentView.opponentRatings||{},tacticType:Number(currentView.tactic?.tacticType??0),tacticLevel:Number(currentView.tactic?.tacticLevel??0),preferredFormation:selectedFormation};
      const r=await fetch('/api/recommend',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({players:team.players,opponent,simulations:10000,isHome:!!currentView.isHome})});
      const x=await jsonResponse(r);if(!r.ok)throw new Error(x.message||'Seçilen diziliş için kadro oluşturulamadı.');
      x.lineup=(x.lineup||[]).map(p=>({...p,role:roleText(p.roleKey)}));
      currentView.__selectedFormationResult=x;currentView.ownRatings=x.ratings||currentView.ownRatings;
      currentView.ownLineup={formation:x.formation,ratings:x.ratings,playerCount:x.lineup.length,players:x.lineup.map(p=>({name:p.name,role:p.role,roleKey:p.roleKey,rating:p.rating,behaviour:p.behaviour,form:p.form,stamina:p.stamina}))};
      currentView.formation=x.formation;currentView.tactic={tacticName:x.tacticName,tacticType:x.tacticType,tacticLevel:x.tacticLevel};selectedFormation=x.formation;
      renderSelectedFormationResult(x);if(status)status.textContent=`HO Engine • ${x.formation} • ${x.tacticName||'Normal'}`;
    }catch(e){if(status)status.textContent=e.message||'Diziliş hesaplanamadı.';}
    finally{formationBusy=false;if(btn){btn.classList.remove('loading');btn.textContent=selectedFormation||'—';}updateFormationChecks();}
  }

  function renderSelectedFormationResult(x){
    const r=x.ratings||{};const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(r);
    const pill=document.getElementById('ownFormation');if(pill)pill.textContent=x.formation||selectedFormation||'—';const title=document.getElementById('ownLineupTitle');if(title)title.textContent='En iyi 11';
    if(typeof renderPitch==='function')renderPitch('#ownPitch',x.lineup||[],false);
    const source=document.getElementById('ownModeSource');if(source)source.textContent=`HO Engine • ${x.formation} • ${x.tacticName||'Normal'}`;
    if(typeof ratingForm==='function'&&currentView){const home=currentView.isHome?(x.ratings||{}):(currentView.opponentRatings||{});const away=currentView.isHome?(currentView.opponentRatings||{}):(x.ratings||{});ratingForm('#homeRatings','home',home);ratingForm('#awayRatings','away',away);}
  }

  function applyOwnMode(){
    ensureControls();if(selectedMode==='cup'&&!latestCup?.record)selectedMode='best';document.querySelectorAll('#ownLineupMode button').forEach(b=>b.classList.toggle('active',b.dataset.mode===selectedMode));
    if(selectedMode==='best'&&currentView?.__selectedFormationResult){const x=currentView.__selectedFormationResult;selectedFormation=x.formation||selectedFormation;updateFormationChecks();renderSelectedFormationResult(x);return;}
    const r=ratingsOf(selectedMode),players=playersOf(selectedMode),formation=selectedMode==='cup'?(latestCup?.record?.formation||'—'):(currentView?.ownLineup?.formation||'—');
    if(selectedMode==='cup'&&!selectedFormation)selectedFormation=formation;
    const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(r);const pill=document.getElementById('ownFormation');if(pill)pill.textContent=formation;const title=document.getElementById('ownLineupTitle');if(title)title.textContent=selectedMode==='cup'?'Son kupa 11':'En iyi 11';if(typeof renderPitch==='function')renderPitch('#ownPitch',players,false);
    const source=document.getElementById('ownModeSource');if(source){if(selectedMode==='cup'){const f=latestCup?.record?.fixture||{};source.textContent=`Kupa: ${fmtDay(f.matchDate)} • ${typeText(f.matchType)} • ${tacticText(latestCup?.record?.teamData?.tacticType)} Lv.${latestCup?.record?.teamData?.tacticLevel??'—'}`;}else source.textContent=`Lig analizi • HO Engine en iyi 11${formation&&formation!=='—'?` • ${formation}`:''}`;}
    if(currentView){if(!currentView.__bestTactic)currentView.__bestTactic=currentView.tactic||{};if(selectedMode==='cup')currentView.tactic={tacticType:latestCup.record.teamData.tacticType??0,tacticLevel:latestCup.record.teamData.tacticLevel??0,tacticName:tacticText(latestCup.record.teamData.tacticType)};else currentView.tactic=currentView.__bestTactic;const home=currentView.isHome?r:(currentView.opponentRatings||{}),away=currentView.isHome?(currentView.opponentRatings||{}):r;if(typeof ratingForm==='function'){ratingForm('#homeRatings','home',home);ratingForm('#awayRatings','away',away);}}
    updateFormationChecks();
  }

  function patchRender(){if(typeof window.renderMatch!=='function')return false;const old=window.renderMatch;if(old.__formationPatched)return true;const wrapped=function(x){old(x);ensureControls();selectedFormation=x?.ownLineup?.formation||selectedFormation;updateFormationChecks();applyOwnMode();decorateOpponentSource();};wrapped.__formationPatched=true;window.renderMatch=wrapped;return true;}
  function decorateOpponentSource(){if(typeof currentView==='undefined'||!currentView)return;const i=Number(currentView.selectedRecentIndex??0),m=currentView.recentMatches?.[i],bar=document.getElementById('recentSelection');if(!bar||!m?.fixture)return;const f=m.fixture;bar.innerHTML=`<b>Simülasyon baz alınan rakip maçı:</b> ${fmtDate(f.matchDate)} • ${esc(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName)} <span>• ${typeText(f.matchType)} • ${esc(m.opponent?.formation||'—')} • ${tacticText(m.opponent?.tacticType)} ${m.opponent?.tacticLevel?`Lv.${m.opponent.tacticLevel}`:''}</span>`;}
  async function loadCup(){try{const r=await fetch('/api/cup-lineup/latest');const x=await jsonResponse(r);if(!r.ok)throw new Error(x.message||'Kupa kadrosu alınamadı.');latestCup=x;ensureControls();if(!selectedFormation)selectedFormation=x.record?.formation||null;applyOwnMode();}catch(e){const s=document.getElementById('ownModeSource');if(s)s.textContent='Son kupa 11 alınamadı; En iyi 11 kullanılabilir.';}}
  const start=()=>{ensureControls();patchRender();loadCup();setTimeout(()=>{ensureControls();patchRender();decorateOpponentSource();applyOwnMode();},250);};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  window.setOwnAnalysisMode=m=>{selectedMode=m==='cup'?'cup':'best';applyOwnMode();};
})();
