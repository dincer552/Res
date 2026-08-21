(() => {
  const FORMATIONS = ['4-4-2','4-3-3','3-5-2','4-5-1','5-4-1','5-3-2','3-4-3'];
  const roles = {
    '4-4-2':['GK','WB','CD','CD','WB','WING','IM','IM','WING','FWD','FWD'],
    '4-3-3':['GK','WB','CD','CD','WB','IM','IM','IM','FWD','FWD','FWD'],
    '3-5-2':['GK','CD','CD','CD','WING','IM','IM','IM','WING','FWD','FWD'],
    '4-5-1':['GK','WB','CD','CD','WB','WING','IM','IM','IM','WING','FWD'],
    '5-4-1':['GK','WB','CD','CD','CD','WB','WING','IM','IM','WING','FWD'],
    '5-3-2':['GK','WB','CD','CD','CD','WB','IM','IM','FWD','FWD'],
    '3-4-3':['GK','CD','CD','CD','WING','IM','IM','WING','FWD','FWD','FWD']
  };
  const trainingSlots = {
    7:['IM','WING','FWD'], 1:['GK','CD','WB','IM','WING','FWD'], 2:['WING','WB'],
    3:['FWD'], 4:['GK','CD','WB','IM','WING','FWD'], 5:['GK'],
    6:['CD','WB','IM','WING'], 8:['GK','CD','WB','IM','WING','FWD'],
    9:['WING','FWD'], 10:['GK','CD','WB','IM','WING','FWD'], 11:['GK','CD','WB','IM','WING','FWD']
  };
  const n = (p,k) => Number(p?.[k] ?? 0);
  const skillFor = (p,k) => k==='GK' ? n(p,'keeper') || n(p,'goalkeeping') :
    k==='CD' || k==='WB' ? n(p,'defending') :
    k==='WING' ? n(p,'winger') : k==='IM' ? n(p,'playmaking') : n(p,'scoring');
  const natural = (p,k) => {
    if (k === 'GK') return skillFor(p,k) >= 5;
    const s = skillFor(p,k);
    const a = Math.max(n(p,'defending'),n(p,'playmaking'),n(p,'winger'),n(p,'scoring'));
    return s >= 6 && s >= a * (k === 'WING' ? .75 : .80);
  };
  const pos = (p,k) => {
    if (k==='GK') return skillFor(p,k);
    if (k==='CD') return n(p,'defending') + n(p,'passing')*.10 + n(p,'playmaking')*.20 + n(p,'winger')*.05;
    if (k==='WB') return n(p,'defending') + n(p,'passing')*.10 + n(p,'playmaking')*.20 + n(p,'winger')*.20;
    if (k==='IM') return n(p,'playmaking') + n(p,'passing')*.20 + n(p,'defending')*.15 + n(p,'winger')*.10 + n(p,'stamina')*.05;
    if (k==='WING') return n(p,'winger') + n(p,'playmaking')*.35 + n(p,'passing')*.15 + n(p,'defending')*.10;
    return n(p,'scoring') + n(p,'passing')*.25 + n(p,'winger')*.15 + n(p,'playmaking')*.10;
  };
  // Training selection is deliberately much more age-sensitive than match selection.
  // A player older than 28 is not allowed to consume a cup training slot when a
  // younger natural candidate exists; this prevents veterans such as Francisco
  // Manuel and Utku Hakan Basak from occupying the training places.
  const trainingAgeEligible = p => n(p,'age') <= 28;
  const ageTrainingBonus = age => age<=17?40:age===18?35:age===19?30:age===20?25:age===21?21:age===22?18:age===23?15:age===24?12:age===25?9:age===26?6:age===27?3:age===28?1:0;
  const trainingScore = (p,type,k) => (trainingSlots[type] || []).includes(k) && trainingAgeEligible(p)
    ? skillFor(p,k)*10 + n(p,'passing')*1.5 + ageTrainingBonus(n(p,'age')) : -100000;
  const opponentBias = (p,k,r={}) => {
    const own = skillFor(p,k);
    if (k==='FWD') return (10-(n(r,'centralDefence')||n(r,'centralDefense')))*own*.25 + (10-(n(r,'leftDefence')+n(r,'rightDefence'))/2)*own*.10;
    if (k==='WING') return (10-(n(r,'leftDefence')+n(r,'rightDefence'))/2)*own*.25;
    if (k==='IM') return (10-n(r,'midfield'))*own*.12;
    if (k==='CD') return (n(r,'centralAttack')||n(r,'centralAttacks'))*own*.10;
    if (k==='WB') return ((n(r,'leftAttack')+n(r,'rightAttack'))/2)*own*.08;
    return 0;
  };

  function chooseCup(players, formation, trainingType, leagueLineup, opponentRatings) {
    const rs = roles[formation];
    const eligible = (players || []).filter(p => !p.injured && !p.suspended);
    const leagueIds = new Set((leagueLineup || []).map(p => Number(p.playerId)));
    const trainRoles = new Set(trainingSlots[trainingType] || []);
    const used = new Set();
    const out = new Array(rs.length).fill(null);

    // Training slots are a hard priority: use a non-league, age-eligible,
    // natural trainee first. Match-only positions can still use veterans.
    const candidates = eligible.flatMap(p => rs.map((k,i) => ({
      p,k,i,
      isTraining: trainRoles.has(k),
      ageEligible: trainingAgeEligible(p),
      value: trainRoles.has(k)
        ? trainingScore(p,trainingType,k) + pos(p,k) + opponentBias(p,k,opponentRatings)
        : pos(p,k) + opponentBias(p,k,opponentRatings),
      isLeague: leagueIds.has(Number(p.playerId))
    })))
      .filter(x => x.value > 0)
      .filter(x => !x.isTraining || (x.ageEligible && !x.isLeague))
      .sort((a,b) => {
        if (a.isTraining !== b.isTraining) return a.isTraining ? -1 : 1;
        return b.value-a.value;
      });

    for (const c of candidates) {
      const id = Number(c.p.playerId);
      if (out[c.i] || used.has(id)) continue;
      if (!natural(c.p,c.k)) continue;
      out[c.i] = c.p;
      used.add(id);
    }

    for (let i=0;i<rs.length;i++) {
      if (out[i]) continue;
      const k = rs[i];
      const isTraining = trainRoles.has(k);
      let pool = eligible.filter(p => !used.has(Number(p.playerId)));
      if (isTraining) {
        // Never let a veteran or a league XI player consume a training slot
        // while an age-eligible non-league candidate exists.
        const preferred = pool.filter(p => trainingAgeEligible(p) && !leagueIds.has(Number(p.playerId)));
        if (preferred.length) pool = preferred;
        else {
          const young = pool.filter(trainingAgeEligible);
          if (young.length) pool = young;
        }
      }
      pool.sort((a,b) => {
        const na = natural(a,k) ? 1 : 0;
        const nb = natural(b,k) ? 1 : 0;
        const va = isTraining ? trainingScore(a,trainingType,k)+pos(a,k)+opponentBias(a,k,opponentRatings) : pos(a,k)+opponentBias(a,k,opponentRatings);
        const vb = isTraining ? trainingScore(b,trainingType,k)+pos(b,k)+opponentBias(b,k,opponentRatings) : pos(b,k)+opponentBias(b,k,opponentRatings);
        return nb-na || vb-va;
      });
      if (pool[0]) { out[i]=pool[0]; used.add(Number(pool[0].playerId)); }
    }
    return out.every(Boolean) ? out : null;
  }

  async function json(url, options) {
    const r = await fetch(url, options);
    const x = await r.json();
    if (!r.ok) throw new Error(x.message || 'İstek başarısız');
    return x;
  }

  function pickRecent(view,label){
    const ms=view?.recentMatches||[];
    if(!ms.length) return 0;
    const text=ms.map((m,i)=>`${i}: ${m.fixture?.homeTeamName||''} ${m.fixture?.homeGoals??'-'}-${m.fixture?.awayGoals??'-'} ${m.fixture?.awayTeamName||''}`).join('\n');
    const v=prompt(`${label}\n${text}\n\nNumara:`,String(view.selectedRecentIndex??0));
    const i=Number(v); return Number.isInteger(i)&&i>=0&&i<ms.length?i:Number(view.selectedRecentIndex??0);
  }
  function pickFormation(label,current){
    const v=prompt(`${label}\n${FORMATIONS.map((x,i)=>`${i+1}: ${x}`).join('\n')}\n\nNumara:`,String(Math.max(1,FORMATIONS.indexOf(current)+1)));
    const i=Number(v)-1; return FORMATIONS[i]||current||'3-4-3';
  }

  function render(mode) {
    const plan=window.__integratedPlan;
    const x=mode==='cup'?plan?.cup:plan?.league;
    if(!x?.result?.lineup?.length) return;
    currentView.ownLineup={formation:x.formation,ratings:x.result.ratings||{},playerCount:11,players:x.result.lineup};
    const title=document.getElementById('ownLineupTitle'); if(title) title.textContent=mode==='cup'?'Kupa Kadrosu':'Lig Kadrosu';
    const f=document.getElementById('ownFormation'); if(f) f.textContent=x.formation;
    if(typeof renderPitch==='function') renderPitch('#ownPitch',x.result.lineup,false);
    if(typeof renderRatingSummary==='function') { const s=document.getElementById('ownRatingStrip'); if(s) s.innerHTML=renderRatingSummary(x.result.ratings||{}); }
    document.querySelectorAll('#ownLineupMode button').forEach(b=>b.classList.toggle('active',b.dataset.mode===(mode==='cup'?'cup':'best')));
  }

  async function fixedRun() {
    const b=document.getElementById('integratedPlanButton');
    if(!b) return;
    try {
      b.disabled=true; b.textContent='Plan hesaplanıyor…';
      const leagueMatchId=currentView?.fixture?.matchId;
      if(!leagueMatchId) throw new Error('Önce lig maçını seç.');
      const leagueIndex=pickRecent(currentView,'LİG RAKİBİNİN BAZ ALINACAK MAÇI');
      const leagueFormation=pickFormation('LİG FORMASYONU',currentView.formation||'3-5-2');
      const leagueView=await json(`/api/fixture-view/${leagueMatchId}?recentIndex=${leagueIndex}`);
      const training=currentView?.training||leagueView?.training;
      if(!training) throw new Error('Antrenman bilgisi alınamadı.');
      const fixtures=await json('/api/fixtures');
      const cups=(fixtures.fixtures||[]).filter(f=>[3,7].includes(Number(f.matchType)));
      if(!cups.length) throw new Error('Yaklaşan kupa maçı bulunamadı.');
      const cupText=cups.map((f,i)=>`${i}: ${f.homeTeamName||''} - ${f.awayTeamName||''}`).join('\n');
      const ci=Number(prompt(`KUPA MAÇI\n${cupText}\n\nNumara:`,'0'));
      const cupFixture=cups[Number.isInteger(ci)&&ci>=0&&ci<cups.length?ci:0];
      const cupBase=await json(`/api/fixture-view/${cupFixture.matchId}?recentIndex=0`);
      const cupIndex=pickRecent(cupBase,'KUPA RAKİBİNİN BAZ ALINACAK MAÇI');
      const cupFormation=pickFormation('KUPA FORMASYONU',cupBase.formation||'3-4-3');
      if(!confirm(`Antrenman: ${training.trainingName||'Antrenman'}\nAktif antrenman bilgisiyle kupa kadrosu oluşturulsun mu?`)) throw new Error('Antrenman onayı verilmedi.');
      const cupView=await json(`/api/fixture-view/${cupFixture.matchId}?recentIndex=${cupIndex}`);
      const team=await json('/api/team');
      const leagueOpp={teamName:leagueView.opponentTeam?.teamName||'Rakip',ratings:leagueView.opponentRatings||{},tacticType:Number(leagueView.tactic?.tacticType||0),tacticLevel:Number(leagueView.tactic?.tacticLevel||0),preferredFormation:leagueFormation};
      const lr=await json('/api/recommend',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({players:team.players,opponent:leagueOpp,simulations:10000,isHome:!!leagueView.isHome})});
      const leagueLine=lr.lineup||[];
      const cupPlayers=chooseCup(team.players,cupFormation,Number(training.trainingType),leagueLine,cupView.opponentRatings||{});
      if(!cupPlayers) throw new Error('Kupa için 11 oyuncu oluşturulamadı.');
      const cupOpp={teamName:cupView.opponentTeam?.teamName||'Kupa Rakibi',ratings:cupView.opponentRatings||{},tacticType:Number(cupView.tactic?.tacticType||0),tacticLevel:Number(cupView.tactic?.tacticLevel||0),preferredFormation:cupFormation};
      const cr=await json('/api/recommend',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({players:cupPlayers,opponent:cupOpp,simulations:10000,isHome:!!cupView.isHome})});
      window.__integratedPlan={league:{matchId:leagueMatchId,recentIndex:leagueIndex,formation:leagueFormation,result:lr},cup:{matchId:cupFixture.matchId,recentIndex:cupIndex,formation:cupFormation,result:cr,selectedPlayerIds:cupPlayers.map(p=>Number(p.playerId))},training};
      render('league');
      render('cup');
      const copy=document.getElementById('copyIntegratedPlanButton'); if(copy) copy.disabled=false;
      b.textContent='Tekrar Hesapla'; b.disabled=false;
    } catch(e) {
      console.error('INTEGRATED FIX FAILED',e);
      b.disabled=false; b.textContent=window.__integratedPlan?'Tekrar Hesapla':'Kadro Planını Hesapla';
      alert(e.message||'Kadro hesabı başarısız.');
    }
  }
  function install() {
    const b=document.getElementById('integratedPlanButton');
    if(!b || b.dataset.fixedBound) return;
    b.dataset.fixedBound='1';
    b.addEventListener('click', e => { e.preventDefault(); e.stopImmediatePropagation(); fixedRun(); }, true);
  }
  const observer=new MutationObserver(install);
  observer.observe(document.documentElement,{childList:true,subtree:true});
  document.addEventListener('DOMContentLoaded',()=>setTimeout(install,500));
  setInterval(install,1000);
})();
