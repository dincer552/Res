(function () {
  'use strict';
  const existing = document.getElementById('v5FormationCompetitionBox');
  if (existing) return;

  const box = document.createElement('section');
  box.id = 'v5FormationCompetitionBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = '<button id="v5FormationCompetitionToggle" type="button" aria-expanded="true" style="width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer;text-align:left"><span>🏆 Formation Competition</span><span id="v5FormationCompetitionArrow" aria-hidden="true">⌃</span></button><div id="v5FormationCompetitionBody" style="padding:10px 12px;border-top:1px solid #e5ebe7"></div>';
  const deployBox = document.getElementById('deployLogBox');
  if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
  else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);

  const body = document.getElementById('v5FormationCompetitionBody');
  const toggle = document.getElementById('v5FormationCompetitionToggle');
  const arrow = document.getElementById('v5FormationCompetitionArrow');
  const esc = value => String(value ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const fmt = value => Number.isFinite(Number(value)) ? Number(value).toFixed(3) : '—';

  let open = true;
  toggle.onclick = function () {
    open = !open;
    body.style.display = open ? 'block' : 'none';
    arrow.textContent = open ? '⌃' : '⌄';
    toggle.setAttribute('aria-expanded', String(open));
  };

  function simplifyQuestionCard() {
    const kicker = document.querySelector('.question-card .question-kicker');
    if (kicker) kicker.textContent = 'SEÇİMLER';
    const title = document.querySelector('.question-card .question-title');
    if (title) title.remove();
    const sub = document.querySelector('.question-card .question-sub');
    if (sub) sub.remove();
    const note = document.querySelector('.question-card .skip-note');
    if (note) note.remove();
    const options = document.querySelector('.question-card .options');
    if (options) options.style.gridTemplateColumns = '1fr';
  }

  simplifyQuestionCard();
  window.addEventListener('DOMContentLoaded', simplifyQuestionCard);
  setTimeout(simplifyQuestionCard, 100);
  setTimeout(simplifyQuestionCard, 500);

  function markAnalysisButton() {
    const button = document.getElementById('analyze');
    if (button && !button.disabled) button.textContent = 'TEKRAR ANALİZ ET';
  }

  function render(data) {
    const rows = data?.m10Decision?.formationCompetition || data?.motorPipeline?.m10?.formationCompetition || [];
    if (!rows.length) {
      body.innerHTML = '<div style="color:#7a827d;font:11px Arial">Formation competition sonucu analiz sonrası burada görünecek.</div>';
      return;
    }
    body.innerHTML = rows.map(row => {
      const status = row.searchDepthStatus === 0 || row.searchDepthStatus === 'Insufficient'
        ? 'SEARCH-DEPTH INSUFFICIENT'
        : 'SEARCH-DEPTH OK';
      const statusStyle = status.includes('INSUFFICIENT') ? '#b33b32' : '#267448';
      return '<div style="display:grid;grid-template-columns:30px 58px 1fr auto;gap:8px;align-items:center;padding:9px 2px;border-bottom:1px solid #edf0ee;font:11px Arial">' +
        '<b style="font-size:12px">#'+esc(row.rank)+'</b>' +
        '<b style="font-size:12px;color:#27322d">'+esc(row.formation)+'</b>' +
        '<div><div style="font-weight:800;color:#59625d">'+esc(row.candidateCount)+' aday • Composite '+fmt(row.compositeScore)+'</div><div style="margin-top:2px;color:#7a827d">Tactical '+fmt(row.tacticalScore)+' • Win '+fmt(Number(row.winProbability)*100)+'%</div></div>' +
        '<div style="text-align:right"><div style="font-weight:900;color:'+statusStyle+'">'+status+'</div><div style="margin-top:2px;color:#7a827d">Δ '+fmt(row.marginVsNext)+'</div></div>' +
        '</div>';
    }).join('') + '<div style="padding-top:9px;color:#7a827d;font:11px Arial">Son sıralama M10 composite skoruna göredir; depth ayrı kontrol edilir.</div>';
  }

  render(null);
  window.addEventListener('v5:analysis-ready', event => {
    markAnalysisButton();
    render(event.detail);
  });

  const originalFetch = window.fetch;
  window.fetch = async function () {
    const response = await originalFetch.apply(this, arguments);
    try {
      const input = arguments[0], url = typeof input === 'string' ? input : input?.url || '';
      if (String(url).includes('/api/v5/analysis') && response.ok) {
        response.clone().json().then(data => {
          markAnalysisButton();
          render(data);
        }).catch(() => {});
      }
    } catch (_) {}
    return response;
  };
})();
