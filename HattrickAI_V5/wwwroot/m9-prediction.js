(function () {
  'use strict';
  if (document.getElementById('v5M9PredictionBox')) return;

  const box = document.createElement('section');
  box.id = 'v5M9PredictionBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = '<div style="padding:13px 16px;border-bottom:1px solid #e5ebe7;font:900 14px Arial;color:#27322d">🎯 M9 Maç Tahmini</div><div id="v5M9PredictionBody" style="padding:14px"></div>';

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
  const first = (...values) => values.find(value => value !== undefined && value !== null);

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

    /* The full event-aware simulation lives on the nested MatchPrediction result.
       Prefer that over the legacy/reconstructed wrapper simulation. */
    const sim = first(p?.simulation, m9?.simulation);
    const simScore = sim?.mostLikelyScore || '—';
    const simScorePct = sim ? pct(sim.mostLikelyScoreProbability) : '—';
    const simResult = sim?.mostLikelyResult || '—';
    const simWin = sim?.outcome?.winProbability;
    const simDraw = sim?.outcome?.drawProbability;
    const simLoss = sim?.outcome?.lossProbability;

    const events = first(m9?.eventGoals, p?.eventGoals, m9?.EventGoals, p?.EventGoals) || {};
    const contributions = Array.isArray(events.contributions || events.Contributions) ? (events.contributions || events.Contributions) : [];
    const playerEvents = first(events.expectedPlayerBasedEvents, events.ExpectedPlayerBasedEvents);
    const teamEvents = first(events.expectedTeamBasedEvents, events.ExpectedTeamBasedEvents);
    const playerXg = first(events.playerBasedSpecialEventGoals, events.PlayerBasedSpecialEventGoals);
    const teamXg = first(events.teamBasedSpecialEventGoals, events.TeamBasedSpecialEventGoals);
    const pnfXg = first(events.powerfulNormalForwardGoals, events.PowerfulNormalForwardGoals);
    const caXg = first(events.counterAttackGoals, events.CounterAttackGoals);
    const lsXg = first(events.longShotGoals, events.LongShotGoals);
    const ogXg = first(events.expectedGoalsConcededFromOwnGoalEvents, events.ExpectedGoalsConcededFromOwnGoalEvents);
    const pressSupp = first(events.pressingSuppressionSignal, events.PressingSuppressionSignal);
    const calStatus = first(events.calibrationStatus, events.CalibrationStatus) || 'Kalibrasyon bekliyor';
    const notes = first(events.notes, events.Notes) || '';

    const resultStyle = predicted === 'Galibiyet' ? '#267448' : predicted === 'Rakip Galibiyeti' ? '#b33b32' : '#8a6d1d';
    const simCount = first(sim?.simulationCount, sim?.SimulationCount, sim?.totalRuns, sim?.TotalRuns, 1000);
    const tickCount = first(sim?.tickCount, sim?.TickCount, 18);

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
        '<div style="font:900 12px Arial;color:#27322d">⚙️ Event → Goal Motoru</div>' +
        '<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:7px;margin-top:9px">' +
          metric('Oyuncu event E', num(playerEvents)) + metric('Takım event E', num(teamEvents)) +
          metric('Oyuncu event xG', num(playerXg)) + metric('Takım event xG', num(teamXg)) +
          metric('PNF xG', num(pnfXg)) + metric('CA xG', num(caXg)) +
          metric('Long Shot xG', num(lsXg)) + metric('Own Goal xG', num(ogXg)) +
          metric('Pressing suppression', pct(pressSupp)) + metric('Calibration', esc(calStatus)) +
        '</div>' +
        eventContributionTable(contributions) +
        '<div style="margin-top:8px;color:#7a827d;font:10px/1.45 Arial">Appendix C.1/C.2 utilityleri hazır. Hidden taker/RT girdileri ve tarihsel calibration tamamlanmadan production coefficient olarak uygulanmıyor.</div>' +
        (notes ? '<div style="margin-top:5px;color:#7a827d;font:10px/1.45 Arial">'+esc(notes)+'</div>' : '') +
      '</div>' +
      '<div style="margin-top:12px;padding:11px;background:#fafbfa;border-radius:10px;border:1px solid #edf0ee">' +
        '<div style="font:900 12px Arial;color:#27322d">🎲 '+esc(simCount)+' maç × '+esc(tickCount)+' tick (5 dk)</div>' +
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:9px">' +
          card('Sim. galibiyet', pct(simWin), '#267448') + card('Sim. beraberlik', pct(simDraw), '#8a6d1d') + card('Sim. rakip', pct(simLoss), '#b33b32') +
        '</div>' +
        '<div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:9px">' +
          metric('En sık skor', esc(simScore) + ' ('+esc(simScorePct)+')') + metric('En sık sonuç', esc(outcomeLabel(simResult))) +
        '</div>' +
        scenarioTable(sim?.scenarios) +
        '<div style="margin-top:8px;padding:8px 9px;background:#f4f7f4;border-radius:8px;color:#747c76;font:10px/1.4 Arial">M10 formation ranking henüz MC sonuçlarını karar fonksiyonuna katmıyor; bu panel MC'yi teşhis/kanıt çıktısı olarak gösterir.</div>' +
      '</div>';
  }

  function eventContributionTable(items) {
    if (!Array.isArray(items) || !items.length) return '<div style="margin-top:9px;color:#8a928d;font:10px Arial">Event contribution verisi bu sonuçta bulunmuyor.</div>';
    let html = '<div style="margin-top:10px;font:10px Arial;color:#7a827d"><b>Event katkıları</b></div><div style="margin-top:5px;overflow:auto"><table style="width:100%;border-collapse:collapse;font:10px Arial;color:#59625d"><tr><th style="text-align:left;padding:5px 3px">Event</th><th style="text-align:right;padding:5px 3px">E</th><th style="text-align:right;padding:5px 3px">Gol %</th><th style="text-align:right;padding:5px 3px">xG</th></tr>';
    items.forEach(x => {
      const name = first(x.event, x.Event, '—');
      const expected = first(x.expectedEvents, x.ExpectedEvents);
      const goalProb = first(x.goalProbability, x.GoalProbability);
      const goals = first(x.expectedGoals, x.ExpectedGoals);
      html += '<tr><td style="padding:4px 3px;border-top:1px solid #edf0ee">'+esc(name)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right">'+num(expected)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right">'+pct(goalProb)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right;font-weight:800">'+num(goals)+'</td></tr>';
    });
    return html + '</table></div>';
  }

  function scenarioTable(scenarios) {
    if (!Array.isArray(scenarios) || !scenarios.length) return '';
    let html = '<div style="margin-top:10px;font:10px Arial;color:#7a827d"><b>Senaryo dağılımları</b></div><div style="margin-top:5px;overflow:auto"><table style="width:100%;border-collapse:collapse;font:10px Arial;color:#59625d"><tr><th style="text-align:left;padding:5px 3px">Koşul</th><th style="text-align:right;padding:5px 3px">Koşu</th><th style="text-align:right;padding:5px 3px">En sık skor</th></tr>';
    scenarios.forEach(s => { html += '<tr><td style="padding:4px 3px;border-top:1px solid #edf0ee">'+esc(s.scenario || s.Scenario)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right">'+esc(s.count ?? s.Count)+'</td><td style="padding:4px 3px;border-top:1px solid #edf0ee;text-align:right;font-weight:800">'+esc(s.mostLikelyScore || s.MostLikelyScore)+'</td></tr>'; });
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
