// Small UI overrides kept separate from the main app bundle.
// Match history cards stay selectable; the explicit simulation-base labels are removed.

function renderRecent(matches){
  const root=$('#recentMatches');
  if(!root)return;
  root.innerHTML=matches.length
    ? matches.map((m,i)=>{
        const f=m.fixture||{};
        const r=m.opponent?.actualMatchRatings||m.opponent?.ratings||m.opponent?.Ratings||emptyRatings;
        const selected=i===selectedRecentIndex;
        return `<button class="recent-card ${selected?'selected':''}" onclick="selectRecent(${i})">
          <div class="recent-top"><span class="radio">${selected?'●':'○'}</span><span>${fmtDay(f.matchDate)}</span><small>${m.opponent?.teamName||m.opponent?.TeamName||''}</small></div>
          <div class="recent-score"><b>${f.homeTeamName||''}</b><strong>${f.homeGoals??'—'} - ${f.awayGoals??'—'}</strong><b>${f.awayTeamName||''}</b></div>
          <div class="recent-stats">MF ${midfield(r).toFixed(2)} • DEF ${avg(r).toFixed(2)} • ATT ${attack(r).toFixed(2)}</div>
          <div class="recent-meta"><span>${m.opponent?.formation||'—'}</span><span>${tacticText(m.opponent?.tacticType)} ${m.opponent?.tacticLevel?`Lv.${m.opponent.tacticLevel}`:''}</span></div>
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

  const root=$('#selectedOpponentMatch');
  if(root){
    root.innerHTML=`<span class="selected-opponent-label">BAZ ALINAN RAKİP MAÇI</span><strong>${fmtDay(f.matchDate)} • ${escapeHtml(home)} ${score} ${escapeHtml(away)}</strong><small>${opponent.formation||'—'} • ${tacticText(opponent.tacticType)} ${opponent.tacticLevel?`Lv.${opponent.tacticLevel}`:''} • MF ${midfield(ratings).toFixed(2)} / DEF ${avg(ratings).toFixed(2)} / ATT ${attack(ratings).toFixed(2)}</small>`;
  }

  renderRecent(currentView.recentMatches);
}
