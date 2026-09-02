(function () {
  'use strict';

  const existing = document.getElementById('v5MotorLogBox');
  if (existing) return;

  const box = document.createElement('section');
  box.id = 'v5MotorLogBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = `
    <button id="v5MotorLogToggle" type="button" style="width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer">
      <span>🧠 V5 Motor Logları • M3 → M10</span><span id="v5MotorLogArrow">⌄</span>
    </button>
    <div id="v5MotorLogBody" style="display:none;border-top:1px solid #e5ebe7">
      <div style="padding:9px 12px;display:flex;justify-content:space-between;align-items:center;color:#747c76;font:11px Arial">
        <span id="v5MotorLogState">Analiz bekleniyor…</span>
        <button id="v5MotorLogRefresh" type="button" style="border:1px solid #d5ddd8;background:#f7f9f7;border-radius:7px;padding:5px 8px;cursor:pointer">↻ Yenile</button>
      </div>
      <div id="v5MotorLogList" style="padding:0 12px 12px"></div>
    </div>`;

  const deployBox = document.getElementById('deployLogBox');
  if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
  else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);

  const body = document.getElementById('v5MotorLogBody');
  const arrow = document.getElementById('v5MotorLogArrow');
  const list = document.getElementById('v5MotorLogList');
  const state = document.getElementById('v5MotorLogState');
  const toggle = document.getElementById('v5MotorLogToggle');
  const refresh = document.getElementById('v5MotorLogRefresh');
  let open = false;
  let lastData = null;

  const esc = value => String(value ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const n = value => Number(value || 0).toFixed(2);
  const orderName = value => ({0:'Normal',1:'Ofansif',2:'Defansif',3:'Merkeze',4:'Kana'})[Number(value)] || String(value ?? '');

  function row(code, title, bodyText, good) {
    return '<div style="border-bottom:1px solid #edf0ee;padding:10px 2px">' +
      '<div style="display:flex;align-items:center;gap:8px"><span style="min-width:42px;padding:4px 6px;border-radius:6px;background:'+(good?'#e5f1e9':'#eef1ef')+';color:#1d7043;font:900 10px Arial;text-align:center">'+esc(code)+'</span><b style="font:800 12px Arial;color:#27322d">'+esc(title)+'</b></div>' +
      '<div style="margin:5px 0 0 50px;color:#707872;font:11px/1.5 Arial">'+esc(bodyText)+'</div></div>';
  }

  function render(data) {
    lastData = data;
    const p = data?.motorPipeline;
    if (!p) {
      state.textContent = 'Motor pipeline sonucu bekleniyor…';
      list.innerHTML = row('M3→M10','Pipeline','Henüz tamamlanmış motor sonucu yok.',false);
      return;
    }

    const m3 = p.m3 || {};
    const m4 = p.m4 || {};
    const m5 = p.m5 || [];
    const m6 = p.m6 || {};
    const m7 = p.m7 || {};
    const m72 = p.m72 || {};
    const m8 = p.m8 || {};
    const m9 = p.m9 || {};
    const m10 = p.m10 || {};
    const plan = p.finalPlan || {};
    const pred = p.finalPrediction || {};

    const orders = (plan.lineup?.slots || []).filter(x => Number(x.playerId) > 0).map(x => (x.playerName || x.playerId)+': '+orderName(x.order)).join(' • ');
    const ratings = plan.rating ? 'DEF '+n(plan.rating.leftDefence)+' / '+n(plan.rating.centralDefence)+' / '+n(plan.rating.rightDefence)+' • MID '+n(plan.rating.midfield)+' • ATT '+n(plan.rating.leftAttack)+' / '+n(plan.rating.centralAttack)+' / '+n(plan.rating.rightAttack) : '—';
    const ranking = (m10.ranking || []).map((x,i) => '#'+(i+1)+' '+(x.formation||'—')+' • composite '+n(x.compositeScore)).join(' | ') || '—';

    list.innerHTML =
      row('M3','Player Analysis',(m3.players?.length || 0)+' oyuncu analiz edildi; uygun pozisyon profilleri üretildi.',true) +
      row('M4','Formation Candidate',(m4.candidates?.length || 0)+' yasal ve doldurulabilir diziliş adayı üretildi.'+(m4.candidates?.[0] ? ' Lider: '+m4.candidates[0].formation+' • structural '+n(m4.candidates[0].structuralScore):''),true) +
      row('M5','Position / XI',m5.length+' XI adayı üretildi. Final M5 adayı: '+(m5[0]?.formation || '—')+' • suitability '+n(m5[0]?.suitabilityScore),true) +
      row('M6','Global Optimization','iterations '+(m6.iterations||0)+' • evaluated '+(m6.evaluatedCandidates||0)+' • retained '+(m6.retainedCandidates||0)+' • '+(m6.converged?'converged':'search completed'),true) +
      row('M7','Regional Ratings',ratings+' • confidence '+(m7.confidence||'—'),true) +
      row('M7.2','Advanced Tactical','tactic '+(m72.tactic||'—')+' • level '+(m72.level?.name||'—')+' '+n(m72.level?.value)+' • chance distribution L/C/R '+n(m72.chanceDistribution?.leftShare)+' / '+n(m72.chanceDistribution?.centreShare)+' / '+n(m72.chanceDistribution?.rightShare),true) +
      row('M8','Chance / Matchup','structural chance '+n(m8.structuralChanceIndex)+' • midfield '+n(m8.midfieldShare)+' • L/C/R attack shares '+n(m8.leftAttackVsRightDefence)+' / '+n(m8.centreAttackVsCentreDefence)+' / '+n(m8.rightAttackVsLeftDefence),true) +
      row('M9','Match Prediction','xG '+n(pred.expectedHomeGoals)+' – '+n(pred.expectedAwayGoals)+' • win '+n(pred.winProbability*100)+'% • draw '+n(pred.drawProbability*100)+'% • loss '+n(pred.lossProbability*100)+'%',true) +
      row('M10','Final Decision',ranking+' • seçilen '+(plan.formation||'—')+' • final orders: '+orders,true);

    state.textContent = 'M3 → M10 tamamlandı • '+(plan.formation || '—')+' seçildi';
  }

  async function load() {
    if (lastData) render(lastData);
  }

  toggle.onclick = function () { open=!open; body.style.display=open?'block':'none'; arrow.textContent=open?'⌃':'⌄'; if(open)load(); };
  refresh.onclick = load;
  render(null);

  const originalFetch = window.fetch;
  window.fetch = async function () {
    const response = await originalFetch.apply(this, arguments);
    try {
      const input = arguments[0];
      const url = typeof input === 'string' ? input : input?.url || '';
      if (String(url).includes('/api/v5/analysis') && response.ok) {
        const clone = response.clone();
        clone.json().then(render).catch(()=>{});
      }
    } catch (_) {}
    return response;
  };
})();
