// Small UI overrides kept separate from the main app bundle.
// Match history cards stay selectable; selected match details use the real match-type logos.

const MATCH_TYPE_ICONS={league:'/match-type-icons/league.svg',cup:'/match-type-icons/cup.svg',friendly:'/match-type-icons/friendly.svg'};
const MATCH_TYPE_LABELS={league:'Lig maçı',cup:'Kupa maçı',friendly:'Hazırlık maçı'};
const MATCH_TYPE_SETS={league:new Set([1,100]),cup:new Set([2,3,7,9,11]),friendly:new Set([4,5,8,12,80,101,103,105,106])};

function matchTypeKind(value){
  const n=Number(value);
  if(MATCH_TYPE_SETS.cup.has(n))return'cup';
  if(MATCH_TYPE_SETS.friendly.has(n))return'friendly';
  return'league';
}

function matchTypeInfo(m){
  const raw=m?.fixture?.matchType ?? m?.fixture?.matchTypeId ?? m?.matchType ?? m?.matchTypeId ?? '';
  const type=String(raw).toLowerCase();
  let kind=matchTypeKind(raw);
  if(type.includes('cup')||type.includes('kupa'))kind='cup';
  else if(type.includes('league')||type.includes('lig'))kind='league';
  else if(type.includes('friendly')||type.includes('hazırlık'))kind='friendly';
  return {kind,icon:MATCH_TYPE_ICONS[kind],label:MATCH_TYPE_LABELS[kind]};
}

function matchTypeIconHtml(m,extraClass='match-type-icon'){
  const info=matchTypeInfo(m);
  return `<img class="${extraClass}" src="${info.icon}" alt="${info.label}" title="${info.label}">`;
}

function renderRecent(matches){
  const root=$('#recentMatches');
  if(!root)return;
  root.innerHTML=matches.length
    ? matches.map((m,i)=>{
        const f=m.fixture||{};
        const r=m.opponent?.actualMatchRatings||m.opponent?.ratings||m.opponent?.Ratings||emptyRatings;
        const selected=i===selectedRecentIndex;
        const type=matchTypeInfo(m);
        return `<button class="recent-card ${selected?'selected':''}" onclick="selectRecent(${i})">
          ${matchTypeIconHtml(m)}
          <div class="recent-top"><span class="radio">${selected?'●':'○'}</span><span>${fmtDay(f.matchDate)}</span><small>${m.opponent?.teamName||m.opponent?.TeamName||''}</small></div>
          <div class="recent-score"><b>${f.homeTeamName||''}</b><strong>${f.homeGoals??'—'} - ${f.awayGoals??'—'}</strong><b>${f.awayTeamName||''}</b></div>
          <div class="recent-stats">MF ${midfield(r).toFixed(2)} • DEF ${avg(r).toFixed(2)} • ATT ${attack(r).toFixed(2)}</div>
          <div class="recent-meta"><span>${type.label}</span><span>${m.opponent?.formation||'—'} • ${tacticText(m.opponent?.tacticType)} ${m.opponent?.tacticLevel?`Lv.${m.opponent.tacticLevel}`:''}</span></div>
        </button>`;
      }).join('')
    : '<div class="empty">Rakibin son maçları bulunamadı.</div>';
}

function applyRecentSelection(i){
  if(!currentView?.recentMatches?.length){
    $('#selectedOpponentMatch').innerHTML='';
    return;
  }

  selectedRecentIndex=Math.max(0,Math.min(i,currentView.recentMatches.length-1));
  const m=currentView.recentMatches[selectedRecentIndex];
  const f=m.fixture||{};
  const opponent=m.opponent||{};
  const ratings=opponent.actualMatchRatings||opponent.ratings||emptyRatings;
  const home=f.homeTeamName||'';
  const away=f.awayTeamName||'';
  const score=`${f.homeGoals??'—'} - ${f.awayGoals??'—'}`;
  const type=matchTypeInfo(m);

  const root=$('#selectedOpponentMatch');
  if(root){
    root.innerHTML=`<div class="selected-opponent-main">${matchTypeIconHtml(m,'selected-match-type-icon')}<div><strong>${fmtDay(f.matchDate)} • ${escapeHtml(home)} ${score} ${escapeHtml(away)}</strong><small>${type.label} • ${opponent.formation||'—'} • ${tacticText(opponent.tacticType)} ${opponent.tacticLevel?`Lv.${opponent.tacticLevel}`:''} • MF ${midfield(ratings).toFixed(2)} / DEF ${avg(ratings).toFixed(2)} / ATT ${attack(ratings).toFixed(2)}</small></div></div>`;
  }

  renderRecent(currentView.recentMatches);
}

function renderScoreDistributionClean(distribution){
  const buckets=['0','1','2','3','4+'];
  const map=new Map((distribution||[]).map(s=>[`${s.home}-${s.away}`,s]));
  let html='<div class="score-matrix-wrap"><table class="score-matrix"><thead><tr><th>Ev / Dep</th>'+buckets.map(b=>`<th>${b}</th>`).join('')+'</tr></thead><tbody>';
  buckets.forEach(home=>{
    html+=`<tr><th>${home}</th>`;
    buckets.forEach(away=>{
      const cell=map.get(`${home}-${away}`)||{count:0,percentage:0};
      html+=`<td title="${cell.count} / ${num(cell.percentage).toFixed(1)}%"><b>${num(cell.percentage).toFixed(1)}%</b><small>${cell.count}</small></td>`;
    });
    html+='</tr>';
  });
  return html+'</tbody></table></div>';
}

async function runSimulation(){
  const result=$('#result');
  result.classList.remove('hidden');
  result.innerHTML='Simülasyon çalışıyor…';
  try{
    const home=getRatings('home'),away=getRatings('away');
    const isHome=!!currentView?.isHome;
    const ownTactic=currentView?.tactic||{};
    const opponentRecent=currentView?.recentMatches?.[selectedRecentIndex]?.opponent||{};
    const homeTactic=isHome?ownTactic:{tacticType:opponentRecent.tacticType??0,tacticLevel:opponentRecent.tacticLevel??0};
    const awayTactic=isHome?{tacticType:opponentRecent.tacticType??0,tacticLevel:opponentRecent.tacticLevel??0}:ownTactic;
    const r=await fetch('/api/simulate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({home,away,simulations:FIXED_SIMULATIONS,homeTacticType:homeTactic.tacticType??0,homeTacticLevel:homeTactic.tacticLevel??0,awayTacticType:awayTactic.tacticType??0,awayTacticLevel:awayTactic.tacticLevel??0})});
    const x=await jsonResponse(r);
    if(!r.ok)throw new Error(x.message||'Hata');

    const fixture=currentView?.fixture||{};
    const homeName=fixture.homeTeamName||'Ev sahibi';
    const awayName=fixture.awayTeamName||'Deplasman';
    const score=String(x.mostLikelyNormalScore||'—');
    const type=matchTypeInfo({fixture});

    result.innerHTML=`<div class="result-grid"><div class="result-stat win"><b>${num(x.homeWinPercentage).toFixed(1)}%</b><span>Ev sahibi</span></div><div class="result-stat draw"><b>${num(x.drawPercentage).toFixed(1)}%</b><span>Beraberlik</span></div><div class="result-stat loss"><b>${num(x.awayWinPercentage).toFixed(1)}%</b><span>Deplasman</span></div></div><div class="result-score-label">En olası normal skor</div><div class="result-score-wrap">${type.kind==='cup'?matchTypeIconHtml({fixture},'result-match-type-icon'):''}<div class="result-score"><span>${escapeHtml(homeName)}</span> <strong>${escapeHtml(score.replace('-', ' - '))}</strong> <span>${escapeHtml(awayName)}</span></div></div><div class="muted">Ortalama ${num(x.averageHomeGoals).toFixed(2)} — ${num(x.averageAwayGoals).toFixed(2)} • ${x.simulations} simülasyon</div><div class="score-dist-title">HO skor dağılım</div>${renderScoreDistributionClean(x.scoreDistribution)}`;
  }catch(e){
    result.textContent=e.message;
  }
}
