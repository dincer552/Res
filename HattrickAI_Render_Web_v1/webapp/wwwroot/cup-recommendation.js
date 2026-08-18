(()=>{
  const FORMATIONS=['4-4-2','4-3-3','3-5-2','4-5-1','5-4-0','5-3-2','3-4-3'];
  const ROLE={GK:'Goalkeeper',LD:'LeftDefender',CD:'CentralDefender',RD:'RightDefender',LW:'LeftWinger',IM:'CentralMidfielder',RW:'RightWinger',LF:'LeftForward',CF:'CentralForward',RF:'RightForward'};
  const roles={
    '4-4-2':[ROLE.GK,ROLE.LD,ROLE.CD,ROLE.CD,ROLE.RD,ROLE.LW,ROLE.IM,ROLE.IM,ROLE.RW,ROLE.LF,ROLE.CF],
    '4-3-3':[ROLE.GK,ROLE.LD,ROLE.CD,ROLE.CD,ROLE.RD,ROLE.IM,ROLE.IM,ROLE.IM,ROLE.LF,ROLE.CF,ROLE.RF],
    '3-5-2':[ROLE.GK,ROLE.CD,ROLE.CD,ROLE.CD,ROLE.LW,ROLE.IM,ROLE.IM,ROLE.IM,ROLE.RW,ROLE.LF,ROLE.CF],
    '4-5-1':[ROLE.GK,ROLE.LD,ROLE.CD,ROLE.CD,ROLE.RD,ROLE.LW,ROLE.IM,ROLE.IM,ROLE.IM,ROLE.RW,ROLE.CF],
    '5-4-1':[ROLE.GK,ROLE.LD,ROLE.CD,ROLE.CD,ROLE.CD,ROLE.RD,ROLE.LW,ROLE.IM,ROLE.IM,ROLE.RW,ROLE.CF],
    '5-3-2':[ROLE.GK,ROLE.LD,ROLE.CD,ROLE.CD,ROLE.CD,ROLE.RD,ROLE.IM,ROLE.IM,ROLE.IM,ROLE.LF,ROLE.CF],
    '3-4-3':[ROLE.GK,ROLE.CD,ROLE.CD,ROLE.CD,ROLE.LW,ROLE.IM,ROLE.IM,ROLE.RW,ROLE.LF,ROLE.CF,ROLE.RF]
  };
  FORMATIONS[4]='5-4-1';
  const roleText=r=>({Goalkeeper:'KL',LeftDefender:'SLB',CentralDefender:'STP',RightDefender:'SGB',LeftWinger:'K',RightWinger:'K',CentralMidfielder:'OM',LeftForward:'SF',CentralForward:'SF',RightForward:'SF'})[r]||'';
  const kind=r=>({Goalkeeper:'GK',LeftDefender:'WB',CentralDefender:'CD',RightDefender:'WB',LeftWinger:'WING',RightWinger:'WING',CentralMidfielder:'IM',LeftForward:'FWD',CentralForward:'FWD',RightForward:'FWD',GK:'GK',SLB:'WB',SGB:'WB',STP:'CD',K:'WING',OM:'IM',SF:'FWD'})[r]||'OTHER';

  const TRAIN={
    0:{name:'Genel',primary:[],secondary:[],skill:'form'},
    1:{name:'Dayanıklılık',primary:['ALL'],secondary:[],skill:'stamina'},
    2:{name:'Duran Toplar',primary:['ALL'],secondary:[],skill:'setPieces'},
    3:{name:'Defans',primary:['CD','WB'],secondary:[],skill:'defending'},
    4:{name:'Golcülük',primary:['FWD'],secondary:[],skill:'scoring'},
    5:{name:'Kanat Hücumu',primary:['WING'],secondary:[],skill:'winger'},
    6:{name:'Şut',primary:['OUTFIELD'],secondary:[],skill:'scoring'},
    7:{name:'Kısa Paslar',primary:['IM','WING','FWD'],secondary:[],skill:'passing'},
    8:{name:'Oyun Kurma',primary:['IM'],secondary:['WING'],skill:'playmaking'},
    9:{name:'Kalecilik',primary:['GK'],secondary:[],skill:'keeper'},
    10:{name:'Ara Paslar',primary:['CD','WB','IM','WING'],secondary:[],skill:'passing'},
    11:{name:'Defansif Pozisyon',primary:['GK','CD','WB','IM','WING'],secondary:[],skill:'defending'}
  };
  const training=type=>TRAIN[Number(type)]||{name:'Antrenman',primary:[],secondary:[],skill:null};
  const effect=(type,role)=>{const k=kind(role),t=training(type);if(t.primary.includes('ALL'))return 1;if(t.primary.includes('OUTFIELD'))return k==='GK'?0:3;if(t.primary.includes(k))return 3;if(t.secondary.includes(k))return 2;return 0;};

  function positionRating(p,role){
    const k=kind(role),d=Number(p.defending||0),pm=Number(p.playmaking||0),w=Number(p.winger||0),pa=Number(p.passing||0),s=Number(p.scoring||0),g=Number(p.keeper||0);
    if(k==='GK')return g*1.5;
    if(k==='CD')return d+pm*.18+pa*.10;
    if(k==='WB')return d+w*.30+pm*.15+pa*.10;
    if(k==='WING')return w+pm*.32+pa*.16+d*.08;
    if(k==='IM')return pm+pa*.22+d*.15+w*.08;
    return s+pa*.25+w*.15+pm*.10;
  }

  function leagueKind(p){return kind(p?.roleKey||p?.role||'');}
  function primarySkill(p,type){const t=training(type);if(t.skill==='form')return Number(p.form||0);if(t.skill==='stamina')return Number(p.stamina||0);if(t.skill==='setPieces')return Number(p.setPieces||0);return Number(p[t.skill]||0);}
  function secondarySkill(p,type){const t=training(type),primary=t.skill;const vals=['keeper','defending','playmaking','winger','passing','scoring','setPieces'].filter(x=>x!==primary).map(x=>Number(p[x]||0));return vals.length?Math.max(...vals):0;}

  // Ranking order: trained skill first, then age/potential, secondary skills, positional quality.
  // Thus a 19yo PM13 correctly beats a 17yo PM6.
  function traineeScore(p,role,type){
    const ps=primarySkill(p,type),age=Math.max(17,Number(p.age||25)),agePotential=Math.max(0,28-age),sec=secondarySkill(p,type),positional=positionRating(p,role);
    return ps*100000 + agePotential*1000 + sec*20 + positional*2 + Number(p.experience||0)*.4 + Number(p.form||0)*.15;
  }
  function normalScore(p,role){return positionRating(p,role)+Number(p.form||0)*.08+Number(p.experience||0)*.03;}
  const healthy=p=>!p?.injured&&!p?.suspended;

  // Produce a transparent test ranking of all healthy players who did not play in the league XI
  // and can receive at least some of the current training in the selected formation.
  function rankTrainingCandidates(players,formation,type,leagueMap){
    const slots=roles[formation].map((role,index)=>({role,index,effect:effect(type,role)})).filter(x=>x.effect>0);
    return players.filter(p=>healthy(p)&&!leagueMap.has(Number(p.playerId))).map(p=>{
      const choices=slots.map(s=>({slot:s,score:traineeScore(p,s.role,type)})).sort((a,b)=>{
        const ae=a.slot.effect===3?1:0,be=b.slot.effect===3?1:0;
        return (be-ae)||(b.score-a.score);
      });
      const best=choices[0];
      if(!best)return null;
      return {
        playerId:p.playerId,
        name:p.name||`#${p.playerId}`,
        age:Number(p.age||0),
        role:roleText(best.slot.role),
        trainingRole:best.slot.role,
        effect:best.slot.effect,
        primarySkill:primarySkill(p,type),
        secondarySkill:secondarySkill(p,type),
        score:Math.round(best.score*100)/100,
        positionRating:Math.round(positionRating(p,best.slot.role)*100)/100
      };
    }).filter(Boolean).sort((a,b)=>b.score-a.score);
  }

  function trainingFormationScore(formation,type){
    const rs=roles[formation];
    let full=0,secondary=0;
    rs.forEach(r=>{const e=effect(type,r);if(e>=3)full++;else if(e===2)secondary++;});
    const experience=Number(currentView?.training?.formationExperience?.[formation]||0);
    return full*10000+secondary*1000+experience*10;
  }
  function chooseFormation(type){return FORMATIONS.slice().sort((a,b)=>trainingFormationScore(b,type)-trainingFormationScore(a,type))[0]||'3-4-3';}

  function bestLeagueForRole(leaguePlayers,slot,used){
    const target=kind(slot.role);
    const exact=leaguePlayers.filter(p=>!used.has(Number(p.playerId))&&healthy(p)&&leagueKind(p)===target);
    exact.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
    return exact[0]||null;
  }

  function bestHealthyFallback(players,slot,used,leagueMap){
    const target=kind(slot.role);
    const exactNonLeague=players.filter(p=>!used.has(Number(p.playerId))&&healthy(p)&&!leagueMap.has(Number(p.playerId))&&leagueKind(p)===target);
    exactNonLeague.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
    if(exactNonLeague[0])return exactNonLeague[0];
    const exactAny=players.filter(p=>!used.has(Number(p.playerId))&&healthy(p)&&leagueKind(p)===target);
    exactAny.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
    if(exactAny[0])return exactAny[0];
    const available=players.filter(p=>!used.has(Number(p.playerId))&&healthy(p));
    available.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
    return available[0]||null;
  }

  function buildLineup(players,formation,type,leagueMap){
    const rs=roles[formation],available=players.filter(healthy),used=new Set(),result=new Array(rs.length).fill(null);
    const trainingRoles=rs.map((role,index)=>({role,index,effect:effect(type,role)})).filter(x=>x.effect>0).sort((a,b)=>b.effect-a.effect);
    const normalRoles=rs.map((role,index)=>({role,index,effect:effect(type,role)})).filter(x=>x.effect<=0);
    const leaguePlayers=[...leagueMap.values()];

    // 1. Non-training areas: keep the same positional role from the league XI.
    // If that player is injured/suspended, use a healthy same-role alternative.
    for(const slot of normalRoles){
      let pick=bestLeagueForRole(leaguePlayers,slot,used);
      if(!pick)pick=bestHealthyFallback(available,slot,used,leagueMap);
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // 2. Training areas: first choose only players who did NOT play in the league XI.
    // The trained skill is the dominant criterion; age is a controlled development bonus.
    const nonLeague=available.filter(p=>!leagueMap.has(Number(p.playerId)));
    for(const slot of trainingRoles){
      const pool=nonLeague.filter(p=>!used.has(Number(p.playerId)));
      pool.sort((a,b)=>traineeScore(b,slot.role,type)-traineeScore(a,slot.role,type));
      const pick=pool[0]||null;
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // 3. Only if the team has too few unused trainees, use league players as a last resort.
    for(const slot of trainingRoles){
      if(result[slot.index])continue;
      const pool=available.filter(p=>!used.has(Number(p.playerId)));
      pool.sort((a,b)=>{
        const al=leagueMap.has(Number(a.playerId)),bl=leagueMap.has(Number(b.playerId));
        return (traineeScore(b,slot.role,type)+(bl?-500000:0))-(traineeScore(a,slot.role,type)+(al?-500000:0));
      });
      const pick=pool[0]||null;
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // 4. Final tactical fill only if something is still empty.
    for(let i=0;i<result.length;i++){
      if(result[i])continue;
      const role=rs[i],pool=available.filter(p=>!used.has(Number(p.playerId))).sort((a,b)=>normalScore(b,role)-normalScore(a,role));
      const pick=pool[0]||null;
      if(pick){used.add(Number(pick.playerId));result[i]={p:pick,role};}
    }
    return result.every(Boolean)?result:null;
  }

  function behaviourFor(lineup){
    const opp=currentView?.opponentRatings||{},ours=currentView?.ownRatings||{};
    const centralDefGap=(opp.centralAttack??0)-(ours.centralDefence??0),midfieldGap=(opp.midfield??0)-(ours.midfield??0),wingDefGap=Math.max((opp.leftAttack??0)-(ours.leftDefence??0),(opp.rightAttack??0)-(ours.rightDefence??0)),centralAtkGap=(ours.centralAttack??0)-(opp.centralDefence??0);
    return lineup.map(({p,role})=>{let b='Normal';if(role===ROLE.IM)b=midfieldGap>.35?'Defensive':(centralAtkGap>.5?'Offensive':'Normal');else if(role===ROLE.LW||role===ROLE.RW)b=wingDefGap>.35?'Defensive':(centralAtkGap>.5?'Offensive':'Normal');else if(role===ROLE.LD||role===ROLE.RD)b=wingDefGap>.45?'Defensive':'Normal';else if(role===ROLE.CD)b=centralDefGap>.45?'Normal':'Offensive';else if([ROLE.LF,ROLE.CF,ROLE.RF].includes(role))b=centralAtkGap<-.45?'Defensive':'Normal';return{...p,roleKey:role,role:roleText(role),behaviour:b,rating:Math.round(positionRating(p,role)*100)/100};});
  }

  async function recommend(){
    if(typeof currentView==='undefined'||!currentView)throw new Error('Maç verisi yok.');
    const team=await fetch('/api/team',{cache:'no-store'}).then(jsonResponse);
    const type=Number(currentView.training?.trainingType??-1);
    const league=currentView.ownLineup?.players||[];
    const leagueMap=new Map(league.map(p=>[Number(p.playerId),p]));
    const formation=chooseFormation(type);
    const players=team.players.map(p=>({...p}));
    const lineup=buildLineup(players,formation,type,leagueMap);
    if(!lineup)throw new Error('Antrenmana uygun 11 oyuncu oluşturulamadı.');
    const final=behaviourFor(lineup);
    const t=training(type);
    const trainingCandidates=rankTrainingCandidates(players,formation,type,leagueMap);
    return {formation,lineup:final,trainingName:currentView.training?.trainingName||t.name,trainingType:type,score:trainingFormationScore(formation,type),trainingCandidates};
  }

  function renderTrainingCandidateList(candidates,trainingName){
    const strip=document.getElementById('ownRatingStrip');
    if(!strip)return;
    if(!candidates?.length){strip.innerHTML=`<div style="padding:8px 0;font-size:13px;opacity:.75">Antrenman için uygun lig dışı aday bulunamadı.</div>`;return;}
    strip.innerHTML=`<div style="width:100%;padding:4px 0 8px"><div style="font-weight:700;font-size:14px;margin-bottom:7px">Antrenman adayları — ${trainingName}</div>`+
      candidates.map((p,i)=>`<div style="display:flex;align-items:center;gap:7px;padding:4px 0;font-size:12px;border-bottom:1px solid rgba(255,255,255,.06)"><b style="width:20px">${i+1}.</b><span style="flex:1"><b>${p.name}</b> · ${p.role}${p.age?` · ${p.age}y`:''}</span><span>${p.primarySkill} ana skill · ${p.effect===3?'%100':'ikincil'} · ${Math.round(p.score)}</span></div>`).join('')+`</div>`;
  }

  async function renderRecommendedCup(e){
    if(e){e.preventDefault();e.stopPropagation();e.stopImmediatePropagation();}
    try{
      const x=await recommend();
      const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML='';
      const pill=document.getElementById('ownFormation');if(pill)pill.textContent=x.formation;
      const title=document.getElementById('ownLineupTitle');if(title)title.textContent='Önerilen Kupa Kadrosu';
      if(typeof renderPitch==='function')renderPitch('#ownPitch',x.lineup,false);
      renderTrainingCandidateList(x.trainingCandidates,x.trainingName);
      window.__recommendedCup=x;window.__recommendedCupActive=true;
      console.table(x.trainingCandidates);
      window.dispatchEvent(new CustomEvent('hattrickai:lineup-updated',{detail:{reason:'recommended-cup',mode:'cup',formation:x.formation,trainingType:x.trainingType}}));
    }catch(err){
      window.__recommendedCupActive=false;
      console.error('Kupa kadrosu önerisi:',err);
    }
    return false;
  }

  function install(){
    const mode=document.getElementById('ownLineupMode');
    if(!mode||mode.dataset.recommendedCupBound)return;
    mode.dataset.recommendedCupBound='1';
    const cup=mode.querySelector('button[data-mode="cup"]'),league=mode.querySelector('button[data-mode="best"]');
    if(cup)cup.addEventListener('click',renderRecommendedCup,true);
    if(league)league.addEventListener('click',()=>{window.__recommendedCupActive=false;window.__recommendedCup=null;},true);
    window.__renderRecommendedCup=renderRecommendedCup;
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(install,300));else setTimeout(install,300);
})();
