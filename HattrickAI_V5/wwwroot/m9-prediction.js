(function () {
  'use strict';
  if (document.getElementById('v5M9PredictionBox')) return;

  const box = document.createElement('section');
  box.id = 'v5M9PredictionBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = '<div style="padding:13px 16px;border-bottom:1px solid #e5ebe7;font:900 14px Arial;color:#27322d">🎯 M9 Maç Tahmini + 1000x Simülasyon</div><div id="v5M9PredictionBody" style="padding:14px"></div>';

  const formationBox = document.getElementById('v5FormationCompetitionBox');
  const deployBox = document.getElementById('deployLogBox');
  if (formationBox && formationBox.parentNode) formationBox.parentNode.insertBefore(box, formationBox);
  else if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
  else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);

  const body = document.getElementById('v5M9PredictionBody');
  const esc = value => String(value ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const pct = value => Number.isFinite(Number(value)) ? (Number(value) * 100).toFixed(1) + '%' : '—';
  const num = value => Number.isFinite(Number(value)) ? Number(value).toFixed(2) : '—';
  const rating = value => Number.isFinite(Number(value)) ? Number(value).toFixed(2) : '—';
  const outcomeLabel = value => value === 'Rakip Galibiyeti' ? 'Rakip kazanır' : value;

  function render(data) {
    const m9 = data?.m9Prediction || data?.m9 || data?.motorPipeline?.m9;
    const p = m9?.prediction || data?.finalPrediction || data?.prediction;
    if (!p) {
      body.innerHTML = '<div style="color:#7a827d;font:11px Arial">M9 sonucu analiz tamamlandıktan sonra burada görünecek.</div>';
      return;
    }

    const win = Number(p.winProbability), draw = Number(p.drawProbability), loss = Number(p.lossProbability);
    const predicted = m9?.predictedResult || (win >= loss ? (win >= draw ? 'Galibiyet' : 'Beraberlik') : (loss >= draw ? 'Rakip Galibiyeti' : 'Beraberlik'));
    const analyticScore = m9?.mostLikelyScore || '—';
    const confidence = m9?.confidenceLabel || '—';
    const own = data?.ownLineup?.teamName || data?.teamName || 'Bizim takım';
    const opponent = data?.opponentName || data?.opponent?.teamName || 'Rakip';
    const ownGoals = Number(p.expectedHomeGoals), oppGoals = Number(p.expectedAwayGoals);
    const sim = m9?.simulation;
    const simScore = sim?.mostLikelyScore || '—';
    const simScorePct = sim ? pct(sim.mostLikelyScoreProbability) : '—';
    const simResult = sim?.mostLikelyResult || '—';
    const simWin = sim?.outcome?.winProbability;
    const simDraw = sim?.outcome?.drawProbability;
    const simLoss = sim?.outcome?.lossProbability;
    const resultStyle = predicted === 'Galibiyet' ? '#267448' : predicted === 'Rakip Galibiyeti' ? '#b33b32' : '#8a6d1d';

    body.innerHTML =
      '<div style="display:flex;justify-content:space-between;gap:12px;align-items:center;flex-wrap:wrap">' +
        '<div><div style="font:900 20px Arial;color:'+resultStyle+'">'+esc(predicted)+'</div>' +
        '<div style="margin-top:4px;color:#59625d;font:800 12px Arial">'+esc(own)+' vs '+esc(opponent)+'</div></div>' +
        '<div style="text-align:right"><div style="font:900 18px Arial;color:#27322d">'+esc(analyticScore)+'</div><div style="color:#7a827d;font:11px Arial">Analitik en olası skor • Güven: '+esc(confidence)+'</div></div>' +
      '</div>' +
      '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:14px">' +
        card('Galibiyet', pct(win), '#267448') + card('Beraberlik', pct(draw), '#8a6d1d') + card('Rakip kazanır', pct(loss), '#b33b32') +
      '</div>' +
      '<div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:10px">' +
        metric('Beklenen gol', num(ownGoals) + ' - ' + num(oppGoals)) +
        metric('Topa sahip olma', pct(p.possessionProbability)) +
      '</div>' +
      '<div style="margin-top:12px;padding:11px;background:#f7f9f7;border-radius:10px;border:1px solid #edf0ee">' +
        '<div style="font:900 12px Arial;color:#27322d">📊 7 Rating / Pozisyon Eşleşmesi</div>' +
        '<div style="margin-top:8px;font:11px/1.65 Arial;color:#59625d">' +
          '<b>MF</b> '+rating(m9?.ownChanceShare*100)+'% şans payı → Rakip '+rating(m9?.opponentChanceShare*100)+'%<br>' +
          '<b>Bizim hücum:</b> Sol '+rating(m9?.ownLeftAttackVsRightDefence)+' → Rakip DEF-R &nbsp;|&nbsp; Merkez '+rating(m9?.ownCentreAttackVsCentreDefence)+' → DEF-C &nbsp;|&nbsp; Sağ '+rating(m9?.ownRightAttackVsLeftDefence)+' → DEF-L<br>' +
          '<b>Rakip tehdidi:</b> Sol '+rating(m9?.opponentLeftAttackVsOwnRightDefence)+' → Biz DEF-R &nbsp;|&nbsp; Merkez '+rating(m9?.opponentCentreAttackVsOwnCentreDefence)+' → DEF-C &nbsp;|&nbsp; Sağ '+rating(m9?.opponentRightAttackVsOwnLeftDefence)+' → DEF-L' +
        '</div></div>' +
      '<div style="margin-top:12px;padding:11px;background:#fafbfa;border-radius:10px;border:1px solid #edf0ee">' +
        '<div style="font:900 12px Arial;color:#27322d">🎲 Monte Carlo Simülasyonu</div>' +
        '<div style="margin-top:4px;color:#7a827d;font:10px Arial">'+esc(String(sim?.simulationCount || 0))+' farklı koşu • venue + chance dağılımı + Poisson gol örneklemesi</div>' +
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:9px">' +
          card('Sim. galibiyet', pct(simWin), '#267448') + card('Sim. beraberlik', pct(simDraw), '#8a6d1d') + card('Sim. rakip', pct(simLoss), '#b33b32') +
        '</div>' +
        '<div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:9px">' +
          metric('1000x en sık skor', esc(simScore) + ' ('+esc(simScorePct)+')') + metric('1000x sonuç', esc(outcomeLabel(simResult))) +
        '</div>' +
        scenarioTable(sim?.scenarios) +
      '</div>' +
      '<div style="margin-top:10px;padding:9px 10px;background:#f7f9f7;border-radius:9px;color:#6d756f;font:11px/1.4 Arial">M9; M7’nin 7 takım ratingini maç çekirdeğine verir. Orta saha fırsat hacmini, karşı koridor hücum-savunma eşleşmesi gol kalitesini belirler. 1000x simülasyon sonucu tek bir rastgele skora değil, dağılımın en sık tekrarına dayanır.</div>';
  }

  function scenarioTable(scenarios) {
    if (!Array.isArray(scenarios) || !scenarios.length) return '';
    let html = '<div style="margin-top:10px;font:10px Arial;color:#7a827d"><b>Senaryo dağılımları</b></div><div style="margin-top:5px;overflow:auto"><table style="width:100%;border-collapse:collapse;font:10px Arial;color:#59625d"><tr><th style="text-align:left;padding:5px 3px">Koşul</th><th style="text-align:right;padding:5px 3px">Koşu</th><th style="text-align:right;padding:5px 3px">En sık skor</th></tr>';
    scenarios.forEach(s => { html += '<tr><td style="padding:4px 3px;border-top:1px solid #edf0ee">'+esc(s.scenario)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right">'+esc(s.count)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right;font-weight:800">'+esc(s.mostLikelyScore)+'</td></tr>'; });
    return html + '</table></div>';
  }

  function card(label, value, color) {
    return '<div style="background:#f7f9f7;border-radius:10px;padding:10px;text-align:center"><div style="color:#7a827d;font:800 10px Arial">'+label+'</div><div style="margin-top:4px;color:'+color+';font:900 18px Arial">'+value+'</div></div>';
  }
  function metric(label, value) {
    return '<div style="background:#fafbfa;border:1px solid #edf0ee;border-radius:9px;padding:9px 10px"><div style="color:#7a827d;font:800 10px Arial">'+label+'</div><div style="margin-top:3px;color:#27322d;font:900 13px Arial">'+value+'</div></div>';
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
