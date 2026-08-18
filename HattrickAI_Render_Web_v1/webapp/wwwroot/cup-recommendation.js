(()=>{
  const FORMATIONS=['4-4-2','4-3-3','3-5-2','4-5-1','5-4-1','5-3-2','3-4-3'];
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
  const roleText=r=>({Goalkeeper:'KL',LeftDefender:'SLB',CentralDefender:'STP',RightDefender:'SGB',LeftWinger:'K',RightWinger:'K',CentralMidfielder:'OM',LeftForward:'SF',CentralForward:'SF',RightForward:'SF'})[r]||'';
  const kind=r=>({Goalkeeper:'GK',LeftDefender:'WB',CentralDefender:'CD',RightDefender:'WB',LeftWinger:'WING',RightWinger:'WING',CentralMidfielder:'IM',LeftForward:'FWD',CentralForward:'FWD',RightForward:'FWD'})[r]||'OTHER';

  // CHPP training types. 3/4/5/6/7/8/9/10/11 are position based; 2 is set pieces.
  const TRAIN={
    0:{name:'Genel',primary:[],secondary:[],skill:'form'},
    1:{name:'Dayanıklılık',primary:['ALL'],secondary:[],skill:'stamina'},
    2:{name:'Duran Toplar',primary:['ALL'],secondary:[],skill:'setPieces'},
    3:{name:'Defans',primary:['CD','WB'],secondary:[],skill:'defending'},
    4:{name:'Golcülük',primary:['FWD'],secondary:[],skill:'scoring'},
    5:{name:'Kanat Hücumu',primary:['WING'],secondary:['WB'],skill:'winger'},
    6:{name:'Şut',primary:['OUTFIELD'],secondary:[],skill:'scoring'},
    7:{name:'Kısa Paslar',primary:['IM','WING','FWD'],secondary:[],skill:'passing'},
    8:{name:'Oyun Kurma',primary:['IM'],secondary:['WING'],skill:'playmaking'},
    9:{name:'Kalecilik',primary:['GK'],secondary:[],skill:'keeper'},
    10:{name:'Ara Paslar',primary:['CD','WB','IM','WING'],secondary:[],skill:'passing'},
    11:{name:'Defansif Pozisyon',primary:['GK','CD','WB','IM','WING'],secondary:[],skill:'defending'}
  };
  const training=type=>TRAIN[Number(type)]||{name:'Antrenman',primary:[],secondary:[],skill:null};
  const effect=(type,role)=>{const k=kind(role),t=training(type);if(t.primary.includes('ALL'))return 1;if(t.primary.includes('OUTFIELD'))return k==='GK'?0:3;if(t.primary.includes(k))return 3;if(t.secondary.includes(k))return 2;return 0;};
  const natural=(p,role)=>{const k=kind(role);if(k==='GK')return Number(p.keeper||0)>=5;if(k==='CD')return Number(p.defending||0)>=5&&Number(p.defending||0)>=Math.max(Number(p.playmaking||0),Number(p.winger||0),Number(p.scoring||0))*.72;if(k==='WB')return Number(p.defending||0)>=5&&Math.max(Number(p.defending||0),Number(p.winger||0))>=Math.max(Number(p.playmaking||0),Number(p.scoring||0))*.70;if(k==='WING')return Number(p.winger||0)>=5&&Number(p.winger||0)>=Math.max(Number(p.defending||0),Number(p.playmaking||0),Number(p.scoring||0))*.70;if(k==='IM')return Number(p.playmaking||0)>=5&&Number(p.playmaking||0)>=Math.max(Number(p.defending||0),Number(p.winger||0),Number(p.scoring||0))*.72;if(k==='FWD')return Number(p.scoring||0)>=5&&Number(p.scoring||0)>=Math.max(Number(p.defending||0),Number(p.playmaking||0),Number(p.winger||0))*.70;return false;};

  function positionRating(p,role){const k=kind(role),d=Number(p.defending||0),pm=Number(p.playmaking||0),w=Number(p.winger||0),pa=Number(p.passing||0),s=Number(p.scoring||0),g=Number(p.keeper||0);if(k==='GK')return g*1.5;if(k==='CD')return d+pm*.18+pa*.10;if(k==='WB')return d+w*.30+pm*.15+pa*.10;if(k==='WING')return w+pm*.32+pa*.16+d*.08;if(k==='IM')return pm+pa*.22+d*.15+w*.08;return s+pa*.25+w*.15+pm*.10;}
  function leagueTrainingRole(leagueMap,id){const p=leagueMap.get(Number(id));return p?.roleKey||p?.role||null;}
  function primarySkill(p,type){const t=training(type);if(t.skill==='form')return Number(p.form||0);if(t.skill==='stamina')return Number(p.stamina||0);if(t.skill==='setPieces')return Number(p.setPieces||0);return Number(p[t.skill]||0);}
  function secondarySkill(p,type){const t=training(type),primary=t.skill;const vals=['keeper','defending','playmaking','winger','passing','scoring','setPieces'].filter(x=>x!==primary).map(x=>Number(p[x]||0));return vals.length?Math.max(...vals):0;}

  // Training candidates are ranked primarily by the skill being trained.
  // Age is a development multiplier, not the main criterion: a 19yo PM13 beats a 17yo PM6.
  function traineeScore(p,role,type){
    const ps=primarySkill(p,type), age=Math.max(17,Number(p.age||25)), agePotential=Math.max(0,28-age), sec=secondarySkill(p,type);
    const roleFit=natural(p,role)?1:0;
    const positional=positionRating(p,role);
    return ps*100 + ps*agePotential*7 + sec*8 + positional*2 + roleFit*55 + Number(p.experience||0)*.4 + Number(p.form||0)*.15;
  }
  function normalScore(p,role){return positionRating(p,role)+Number(p.form||0)*.08+Number(p.experience||0)*.03;}
  const healthy=p=>!p?.injured&&!p?.suspended;
  const uniq=a=>a.filter((p,i)=>p&&a.findIndex(x=>Number(x.playerId)===Number(p.playerId))===i);

  function trainingFormationScore(formation,type,leagueMap){
    const rs=roles[formation];
    let full=0,secondary=0,quality=0;
    rs.forEach(r=>{const e=effect(type,r);if(e>=3)full++;else if(e===2)secondary++;});
    // The first priority is number of full training slots, then secondary slots.
    // This deliberately outranks match strength: the Cup lineup exists to train players.
    const experience=Number(currentView?.training?.formationExperience?.[formation]||0);
    return full*10000+secondary*1000+experience*10;
  }

  function chooseFormation(type,leagueMap){
    return FORMATIONS.slice().sort((a,b)=>trainingFormationScore(b,type,leagueMap)-trainingFormationScore(a,type,leagueMap))[0]||'3-4-3';
  }

  function buildLineup(players,formation,type,leagueMap){
    const rs=roles[formation],available=players.filter(healthy),used=new Set(),outOfTrainingLeague=available.filter(p=>{const lr=leagueTrainingRole(leagueMap,p.playerId);return lr&&effect(type,lr)<=0;});
    const trainingRoles=rs.map((role,index)=>({role,index,effect:effect(type,role)})).filter(x=>x.effect>0).sort((a,b)=>b.effect-a.effect);
    const normalRoles=rs.map((role,index)=>({role,index,effect:effect(type,role)})).filter(x=>x.effect<=0);
    const result=new Array(rs.length).fill(null);

    // PHASE 1: positions outside the training area are filled from the existing league XI.
    // This preserves the best non-training league players and keeps the training pool free.
    for(const slot of normalRoles){
      let pool=outOfTrainingLeague.filter(p=>!used.has(Number(p.playerId)));
      pool.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
      let pick=pool.find(p=>natural(p,slot.role))||pool[0];
      // If the league starter for this area is injured/suspended, use the best healthy alternative.
      if(!pick) {
        pool=available.filter(p=>!used.has(Number(p.playerId))&&(!leagueMap.has(Number(p.playerId))||effect(type,leagueTrainingRole(leagueMap,p.playerId)||'')<=0));
        pool.sort((a,b)=>normalScore(b,slot.role)-normalScore(a,slot.role));
        pick=pool.find(p=>natural(p,slot.role))||pool[0];
      }
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // PHASE 2: training positions are filled first with players who did NOT play in the league XI.
    // Full-training slots are handled before secondary slots.
    const nonLeague=available.filter(p=>!leagueMap.has(Number(p.playerId)));
    for(const slot of trainingRoles){
      let pool=nonLeague.filter(p=>!used.has(Number(p.playerId)));
      pool.sort((a,b)=>traineeScore(b,slot.role,type)-traineeScore(a,slot.role,type));
      let pick=pool.find(p=>natural(p,slot.role))||pool[0];
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // PHASE 3: if the squad is short, use league players only as a fallback.
    // Losing a training slot is worse than a small rating loss, but we never do it when a
    // healthy non-league trainee is available.
    for(const slot of trainingRoles){
      if(result[slot.index])continue;
      let pool=available.filter(p=>!used.has(Number(p.playerId)));
      pool.sort((a,b)=>{
        const al=leagueMap.has(Number(a.playerId)),bl=leagueMap.has(Number(b.playerId));
        const ap=traineeScore(a,slot.role,type)+(al?-5000:0),bp=traineeScore(b,slot.role,type)+(bl?-5000:0);
        return bp-ap;
      });
      const pick=pool.find(p=>natural(p,slot.role))||pool[0];
      if(pick){used.add(Number(pick.playerId));result[slot.index]={p:pick,role:slot.role};}
    }

    // PHASE 4: fill any remaining tactical slot with the best available positional player.
    for(let i=0;i<result.length;i++){
      if(result[i])continue;
      const role=rs[i],pool=available.filter(p=>!used.has(Number(p.playerId))).sort((a,b)=>normalScore(b,role)-normalScore(a,role));
      const pick=pool.find(p=>natural(p,role))||pool[0];
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
    const formation=chooseFormation(type,leagueMap);
    const lineup=buildLineup(team.players.map(p=>({...p})),formation,type,leagueMap);
    if(!lineup)throw new Error('Antrenmana uygun 11 oyuncu oluşturulamadı.');
    const final=behaviourFor(lineup);
    const t=training(type);
    return {formation,lineup:final,trainingName:currentView.training?.trainingName||t.name,trainingType:type,score:trainingFormationScore(formation,type,leagueMap)};
  }

  async function renderRecommendedCup(e){
    if(e){e.preventDefault();e.stopPropagation();e.stopImmediatePropagation();}
    try{
      const x=await recommend();
      const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML='';
      const pill=document.getElementById('ownFormation');if(pill)pill.textContent=x.formation;
      const title=document.getElementById('ownLineupTitle');if(title)title.textContent='Önerilen Kupa Kadrosu';
      if(typeof renderPitch==='function')renderPitch('#ownPitch',x.lineup,false);
      window.__recommendedCup=x;window.__recommendedCupActive=true;
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
