(()=>{
  let latestCup=null;
  let selectedMode='best';
  let selectedFormation=null;
  let ownTeamPromise=null;
  let formationBusy=false;
  let modeResults={best:null,cup:null};

  const FORMATIONS=['4-4-2','4-3-3','3-5-2','4-5-1','5-4-1','5-3-2','3-4-3'];
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const tacticText=t=>({0:'Normal',1:'Pres',2:'Kontra',3:'Ortadan Hücum',4:'Kanatlardan Hücum',7:'Yaratıcı'})[Number(t)]||`Taktik ${t??'?'}`;
  const typeText=t=>{const n=Number(t);if([3,7].includes(n))return'Kupa maçı';if([2].includes(n))return'Eleme maçı';if([4,5,8,9,12].includes(n))return'Hazırlık maçı';return'Lig maçı'};
  const roleText=k=>({Goalkeeper:'KL',LeftDefender:'SLB',CentralDefender:'STP',RightDefender:'SGB',LeftMidfielder:'OS',CentralMidfielder:'OM',RightMidfielder:'OS',LeftWinger:'K',RightWinger:'K',LeftForward:'SF',CentralForward:'SF',RightForward:'SF'})[k]||'';

  function notifyLineupChanged(reason='formation'){
    window.dispatchEvent(new CustomEvent('hattrickai:lineup-updated',{detail:{reason,formation:selectedFormation,mode:selectedMode}}));
  }

  function renderRecommendationLogic(){
    const t=currentView?.training,r=currentView?.recommendation;
    const pill=document.getElementById('trainingNamePill'),note=document.getElementById('trainingRecommendation'),list=document.getElementById('trainingFormationList');
    if(!pill||!note||!list)return;
    if(!t){pill.textContent='CHPP verisi yok';note.textContent='Antrenman bilgisi alınamadı.';list.innerHTML='';return;}
    pill.textContent=t.trainingName||'Antrenman';
    const selected=currentView?.formation||'—',exp=r?.formationExperience??t.formationExperience?.[selected]??0;
    note.innerHTML=`<b>${esc(t.trainingName)}</b> aktif. Motor antrenman uyumu, formasyon deneyimi, rakip ve ratingleri birlikte değerlendirir. Şu an <b>${esc(selected)}</b> • deneyim <b>${exp}</b>.`;
    list.innerHTML=(t.preferredFormations||[]).map((f,i)=>`<span class="${f===selected?'primary':''}">${i+1}. ${esc(f)} • exp ${t.formationExperience?.[f]??0}</span>`).join('');
  }

  function ensureOpponentCopy(){
    const panel=document.querySelector('.lineup-panel:not(.our-panel)');
    const head=panel?.querySelector('.panel-head');
    if(!head||document.getElementById('copyOpponentFormation'))return;
    const b=document.createElement('button');
    b.id='copyOpponentFormation';b.type='button';b.className='own-copy-opponent';b.textContent='Rakibi Kopyala';
    b.title='Rakibin seçili maçtaki dizilişini bizim seçili kadroya uygula';
    b.addEventListener('click',copyOpponentFormation);
    head.appendChild(b);
  }

  function ensureControls(){
    const panel=document.querySelector('.our-panel');if(!panel)return;
    const head=panel.querySelector('.panel-head');if(!head)return;
    if(!document.getElementById('ownLineupMode')){
      const wrap=document.createElement('div');wrap.id='ownLineupMode';wrap.className='own-lineup-mode';
      wrap.innerHTML=`<div class="own-mode-label">KADRO TÜRÜ</div><div class="own-mode-buttons"><button type="button" data-mode="best">Lig Kadrosu</button><button type="button" data-mode="cup">Kupa Kadrosu</button></div><div id="ownModeSource" class="own-mode-source"></div>`;
      head.appendChild(wrap);
      wrap.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>setOwnMode(b.dataset.mode)));
    }
    if(!document.getElementById('ownFormationWrap')){
      const old=document.getElementById('ownFormation');if(!old)return;
      const current=old.textContent?.trim();if(!selectedFormation&&FORMATIONS.includes(current))selectedFormation=current;
      const wrap=document.createElement('div');wrap.id='ownFormationWrap';wrap.className='own-formation-wrap';
      wrap.innerHTML=`<button id="ownFormation" class="own-formation-button" type="button" aria-haspopup="true" aria-expanded="false">${esc(selectedFormation||current||'—')}</button><div id="ownFormationMenu" class="own-formation-menu">${FORMATIONS.map(f=>`<button type="button" class="own-formation-option" data-formation="${f}"><span>${f}</span><span class="own-formation-check"></span></button>`).join('')}</div><div id="ownFormationStatus" class="own-formation-status"></div>`;
      old.replaceWith(wrap);
      wrap.querySelector('#ownFormation').addEventListener('click',toggleFormationMenu);
      wrap.querySelectorAll('.own-formation-option').forEach(b=>b.addEventListener('click',()=>selectFormation(b.dataset.formation)));
      document.addEventListener('click',e=>{if(!wrap.contains(e.target))closeFormationMenu();});
      updateFormationChecks();
    }
    ensureOpponentCopy();
  }

  function toggleFormationMenu(){const menu=document.getElementById('ownFormationMenu'),btn=document.getElementById('ownFormation');if(!menu||!btn)return;const open=!menu.classList.contains('open');menu.classList.toggle('open',open);btn.setAttribute('aria-expanded',String(open));}
  function closeFormationMenu(){const menu=document.getElementById('ownFormationMenu'),btn=document.getElementById('ownFormation');if(!menu||!btn)return;menu.classList.remove('open');btn.setAttribute('aria-expanded','false');}
  function updateFormationChecks(){document.querySelectorAll('.own-formation-option').forEach(b=>{const selected=b.dataset.formation===selectedFormation;b.classList.toggle('selected',selected);const c=b.querySelector('.own-formation-check');if(c)c.textContent=selected?'✓':'';});const btn=document.getElementById('ownFormation');if(btn)btn.textContent=selectedFormation||'—';}

  async function getOwnTeam(){
    if(ownTeamPromise)return ownTeamPromise;
    ownTeamPromise=fetch('/api/team',{cache:'no-store'}).then(jsonResponse).then(x=>{if(!x?.players?.length)throw new Error('Takım kadrosu alınamadı.');return x;}).catch(e=>{ownTeamPromise=null;throw e;});
    return ownTeamPromise;
  }

  async function selectFormation(formation){
    if(!FORMATIONS.includes(formation)||formationBusy)return;
    selectedFormation=formation;updateFormationChecks();closeFormationMenu();
    await runFormationAnalysis();
  }

  async function setOwnMode(mode){
    selectedMode=mode==='cup'?'cup':'best';
    document.querySelectorAll('#ownLineupMode button').forEach(b=>b.classList.toggle('active',b.dataset.mode===selectedMode));
    if(selectedFormation){await runFormationAnalysis();return;}
    applyOwnModeFallback();
  }

  async function runFormationAnalysis(){
    if(!selectedFormation||typeof currentView==='undefined'||!currentView)return;
    formationBusy=true;
    const btn=document.getElementById('ownFormation'),status=document.getElementById('ownFormationStatus');
    if(btn){btn.classList.add('loading');btn.textContent='…';}
    if(status)status.textContent=`${selectedFormation} • ${selectedMode==='cup'?'Kupa':'Lig'} kadrosu hesaplanıyor…`;
    try{
      const team=await getOwnTeam();
      const recent=currentView.recentMatches?.[Number(currentView.selectedRecentIndex??0)];
      const opponent={
        teamName:currentView.opponentTeam?.teamName||currentView.opponentTeam?.TeamName||'Rakip',
        ratings:currentView.opponentRatings||{},
        tacticType:Number(recent?.opponent?.tacticType??currentView.tactic?.tacticType??0),
        tacticLevel:Number(recent?.opponent?.tacticLevel??currentView.tactic?.tacticLevel??0),
        preferredFormation:selectedFormation
      };
      const r=await fetch('/api/recommend',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({players:team.players,opponent,simulations:10000,isHome:!!currentView.isHome})});
      const x=await jsonResponse(r);if(!r.ok)throw new Error(x.message||'Seçilen diziliş için kadro oluşturulamadı.');
      x.lineup=(x.lineup||[]).map(p=>({...p,role:roleText(p.roleKey)}));
      modeResults[selectedMode]=x;
      selectedFormation=x.formation||selectedFormation;
      currentView.__selectedFormationResults=modeResults;
      currentView.__selectedFormationResult=x;
      currentView.ownRatings=x.ratings||currentView.ownRatings;
      currentView.ownLineup={formation:x.formation,ratings:x.ratings,playerCount:x.lineup.length,players:x.lineup.map(p=>({playerId:p.playerId,name:p.name,role:p.role,roleKey:p.roleKey,rating:p.rating,behaviour:p.behaviour,form:p.form,stamina:p.stamina}))};
      currentView.formation=x.formation;
      currentView.tactic={tacticName:x.tacticName,tacticType:x.tacticType,tacticLevel:x.tacticLevel};
      currentView.recommendation={...(currentView.recommendation||{}),explanation:x.explanation,selectionScore:x.selectionScore,trainingFit:x.trainingFit,formationExperience:x.formationExperience,trainingName:x.trainingName,trainingPriority:x.trainingPriority};
      renderSelectedFormationResult(x);renderRecommendationLogic();notifyLineupChanged('formation');
      if(status)status.textContent=`HO Engine • ${x.formation} • ${x.tacticName||'Normal'}`;
    }catch(e){if(status)status.textContent=e.message||'Diziliş hesaplanamadı.';}
    finally{formationBusy=false;if(btn){btn.classList.remove('loading');btn.textContent=selectedFormation||'—';}updateFormationChecks();}
  }

  function renderSelectedFormationResult(x){
    const r=x?.ratings||{};
    const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(r);
    const pill=document.getElementById('ownFormation');if(pill)pill.textContent=x?.formation||selectedFormation||'—';
    const title=document.getElementById('ownLineupTitle');if(title)title.textContent=selectedMode==='cup'?'Kupa Kadrosu':'Lig Kadrosu';
    if(typeof renderPitch==='function')renderPitch('#ownPitch',x?.lineup||[],false);
    const source=document.getElementById('ownModeSource');if(source)source.textContent=`${selectedMode==='cup'?'Kupa':'Lig'} • ${x?.formation||selectedFormation}`;
    if(typeof ratingForm==='function'&&currentView){const home=currentView.isHome?r:(currentView.opponentRatings||{}),away=currentView.isHome?(currentView.opponentRatings||{}):r;ratingForm('#homeRatings','home',home);ratingForm('#awayRatings','away',away);}
  }

  function applyOwnModeFallback(){
    ensureControls();
    const x=modeResults[selectedMode];
    if(x){renderSelectedFormationResult(x);return;}
    if(selectedMode==='cup'&&latestCup?.record){
      const r=latestCup.record.teamData?.ratings||{};const players=(latestCup.record.players||[]).map(p=>({name:p.name,role:p.role,roleKey:p.roleKey,rating:p.rating,behaviour:p.behaviour,form:'-',stamina:'-'}));
      const f=latestCup.record.formation||selectedFormation||'—';
      const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(r);
      const pill=document.getElementById('ownFormation');if(pill)pill.textContent=f;const title=document.getElementById('ownLineupTitle');if(title)title.textContent='Kupa Kadrosu';
      if(typeof renderPitch==='function')renderPitch('#ownPitch',players,false);return;
    }
    const f=currentView?.ownLineup?.formation||selectedFormation||'—';if(!selectedFormation)selectedFormation=f;updateFormationChecks();
  }

  function copyOpponentFormation(){
    const formation=currentView?.opponentLineup?.formation||currentView?.recentMatches?.[Number(currentView?.selectedRecentIndex??0)]?.opponent?.formation;
    if(!formation||!FORMATIONS.includes(formation)){const s=document.getElementById('ownFormationStatus');if(s)s.textContent='Rakibin formasyonu alınamadı.';return;}
    selectedFormation=formation;updateFormationChecks();closeFormationMenu();runFormationAnalysis();
  }

  function patchRender(){if(typeof window.renderMatch!=='function')return false;const old=window.renderMatch;if(old.__formationPatched)return true;const wrapped=function(x){old(x);modeResults={best:null,cup:null};selectedFormation=x?.ownLineup?.formation||selectedFormation;ensureControls();updateFormationChecks();applyOwnModeFallback();renderRecommendationLogic();decorateOpponentSource();};wrapped.__formationPatched=true;window.renderMatch=wrapped;return true;}

  function decorateOpponentSource(){if(typeof currentView==='undefined'||!currentView)return;const i=Number(currentView.selectedRecentIndex??0),m=currentView.recentMatches?.[i],bar=document.getElementById('recentSelection');if(!bar||!m?.fixture)return;const f=m.fixture;bar.innerHTML=`<b>Simülasyon baz alınan rakip maçı:</b> ${fmtDate(f.matchDate)} • ${esc(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName)} <span>• ${typeText(f.matchType)} • ${esc(m.opponent?.formation||'—')} • ${tacticText(m.opponent?.tacticType)} ${m.opponent?.tacticLevel?`Lv.${m.opponent.tacticLevel}`:''}</span>`;}

  async function loadCup(){
    try{const r=await fetch('/api/cup-lineup/latest?t='+Date.now(),{cache:'no-store'}),x=await jsonResponse(r);if(!r.ok)throw new Error(x.message||'Kupa kadrosu alınamadı.');if(!x.record?.players||x.record.players.length!==11)throw new Error('Son kupa maçının 11 oyuncusu CHPP’den alınamadı.');latestCup=x;ensureControls();applyOwnModeFallback();}
    catch(e){latestCup=null;}
  }

  const start=()=>{ensureControls();patchRender();loadCup();setTimeout(()=>{ensureControls();patchRender();decorateOpponentSource();applyOwnModeFallback();renderRecommendationLogic();},700);};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  window.setOwnAnalysisMode=setOwnMode;
})();
