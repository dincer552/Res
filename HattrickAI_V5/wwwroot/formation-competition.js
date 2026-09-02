(function () {
  'use strict';
  const existing = document.getElementById('v5FormationCompetitionBox');
  if (existing) return;

  const box = document.createElement('section');
  box.id = 'v5FormationCompetitionBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = '<div style="padding:13px 16px;border-bottom:1px solid #e5ebe7;font:800 13px Arial;color:#27322d">🏆 Formation Competition</div><div id="v5FormationCompetitionBody" style="padding:10px 12px"></div>';
  const deployBox = document.getElementById('deployLogBox');
  if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
  else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);

  const body = document.getElementById('v5FormationCompetitionBody');
  const esc = value => String(value ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const fmt = value => Number.isFinite(Number(value)) ? Number(value).toFixed(3) : '—';

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
  window.addEventListener('v5:analysis-ready', event => render(event.detail));
  const originalFetch = window.fetch;
  window.fetch = async function () {
    const response = await originalFetch.apply(this, arguments);
    try {
      const input = arguments[0], url = typeof input === 'string' ? input : input?.url || '';
      if (String(url).includes('/api/v5/analysis') && response.ok) response.clone().json().then(render).catch(() => {});
    } catch (_) {}
    return response;
  };
})();
