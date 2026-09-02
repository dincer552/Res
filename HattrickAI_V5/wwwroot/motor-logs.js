(function () {
  'use strict';
  const existing = document.getElementById('v5MotorLogBox');
  if (existing) return;
  const box = document.createElement('section');
  box.id = 'v5MotorLogBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
  box.innerHTML = `<button id="v5MotorLogToggle" type="button" style="width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer"><span>🧠 V5 Motor Logları • M3 → M10</span><span id="v5MotorLogArrow">⌄</span></button><div id="v5MotorLogBody" style="display:none;border-top:1px solid #e5ebe7"><div style="padding:9px 12px;display:flex;justify-content:space-between;align-items:center;color:#747c76;font:11px Arial"><span id="v5MotorLogState">Analiz bekleniyor…</span><button id="v5MotorLogRefresh" type="button" style="border:1px solid #d5ddd8;background:#f7f9f7;border-radius:7px;padding:5px 8px;cursor:pointer">↻ Yenile</button></div><div id="v5MotorLogList" style="padding:0 12px 12px"></div></div>`;
  const deployBox = document.getElementById('deployLogBox');
  if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
  else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);

  const body = document.getElementById('v5MotorLogBody');
  const arrow = document.getElementById('v5MotorLogArrow');
  const list = document.getElementById('v5MotorLogList');
  const state = document.getElementById('v5MotorLogState');
  const toggle = document.getElementById('v5MotorLogToggle');
  const refresh = document.getElementById('v5MotorLogRefresh');
  let open = false, timer = null, running = false;
  const esc = value => String(value ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const motors = ['M3','M4','M5','M6','M7','M7.2','M8','M9','M10'];
  const orderName = value => ({0:'Normal',1:'Ofansif',2:'Defansif',3:'Merkeze',4:'Kanada'}[Number(value)] || String(value ?? ''));

  function copyLineup(kind) {
    const data = window.__v5LastAnalysis;
    if (!data) return;
    const lineup = kind === 'opp' ? (data.opponentLineup || data.opponent || {}) : (data.ownLineup || data.own || {});
    const rating = kind === 'opp' ? (data.opponentRating || {}) : (data.ownRating || {});
    const players = (lineup.slots || []).filter(x => Number(x.playerId) > 0);
    if (!players.length) return;

    const title = kind === 'opp' ? 'RAKİP' : 'KULLANICI TAKIMI';
    const lines = [
      'HattrickAI V5 KOPYA',
      `TAKIM: ${lineup.teamName || data.teamName || '—'}`,
      `DİZİLİŞ: ${lineup.formation || '—'}`,
      '',
      'OYUNCULAR:'
    ];
    players.forEach(p => lines.push(`${p.code || p.positionCode || '—'}: ${p.playerName || '—'} | RP=${Number(p.rating || 0).toFixed(1)} | ${orderName(p.order)}`));
    lines.push('', 'OYUNCU TALİMATLARI / DAVRANIŞLAR:');
    players.forEach(p => lines.push(`${p.code || p.positionCode || '—'}: ${orderName(p.order)}`));
    lines.push('', 'BÖLGESEL RATING:');
    lines.push(`DEF-L: ${fmt(rating.leftDefence)}`);
    lines.push(`DEF-C: ${fmt(rating.centralDefence)}`);
    lines.push(`DEF-R: ${fmt(rating.rightDefence)}`);
    lines.push(`MID: ${fmt(rating.midfield)}`);
    lines.push(`ATT-L: ${fmt(rating.leftAttack)}`);
    lines.push(`ATT-C: ${fmt(rating.centralAttack)}`);
    lines.push(`ATT-R: ${fmt(rating.rightAttack)}`);
    lines.push('', `KAYNAK: ${title} final M10 planı`);

    navigator.clipboard.writeText(lines.join('\n')).catch(() => {});
  }
  function fmt(value) { return Number.isFinite(Number(value)) ? Number(value).toFixed(2) : '—'; }

  function icon(status) { if (status === 'completed') return '<span style="color:#267448;font-weight:900">✓</span>'; if (status === 'failed') return '<span style="color:#b33b32;font-weight:900">✕</span>'; if (status === 'running') return '<span style="color:#2f7d4f;font-weight:900">●</span>'; return '<span style="color:#a3aaa5;font-weight:900">○</span>'; }
  function label(status) { return status === 'completed' ? 'Tamamlandı' : status === 'failed' ? 'Hata' : status === 'running' ? 'Çalışıyor' : 'Bekliyor'; }
  function renderUnavailable() {
    state.textContent = running ? 'Analiz başlatıldı • motor run bekleniyor…' : 'Analiz bekleniyor…';
    list.innerHTML = motors.map(m => `<div style="display:flex;align-items:center;gap:10px;border-bottom:1px solid #edf0ee;padding:9px 2px"><span style="width:22px;text-align:center;font-size:16px">○</span><b style="width:38px;font:900 12px Arial;color:#27322d">${m}</b><span style="color:#8a928d;font:11px Arial">Bekliyor</span></div>`).join('');
  }
  function render(data) {
    if (!data?.available || !data.log) return renderUnavailable();
    const log = data.log, byMotor = Object.fromEntries((log.stages || []).map(x => [x.motor, x]));
    const completed = (log.stages || []).filter(x => x.status === 'completed').length;
    const failed = (log.stages || []).find(x => x.status === 'failed');
    const active = (log.stages || []).find(x => x.status === 'running');
    state.textContent = failed ? `❌ ${failed.motor} durdu • ${failed.message}` : log.status === 'completed' ? `🟢 Analiz tamamlandı • ${completed}/${motors.length} motor` : active ? `${active.motor} ● Çalışıyor • ${active.message}` : 'Analiz çalışıyor…';
    state.style.color = failed ? '#b33b32' : log.status === 'completed' ? '#267448' : '#707872';
    list.innerHTML = motors.map(m => {
      const x = byMotor[m] || { motor:m, status:'pending', message:'Bekliyor', durationMs:0 };
      const progress = x.currentIteration && x.maxIterations ? ` • ${x.currentIteration}/${x.maxIterations} iteration` : '';
      const duration = x.durationMs > 0 ? ` • ${(x.durationMs / 1000).toFixed(2)} sn` : '';
      return `<div style="display:flex;align-items:center;gap:10px;border-bottom:1px solid #edf0ee;padding:10px 2px"><span style="width:22px;text-align:center;font-size:16px">${icon(x.status)}</span><b style="width:38px;font:900 12px Arial;color:#27322d">${esc(m)}</b><div style="flex:1;min-width:0"><div style="font:800 11px Arial;color:${x.status === 'failed' ? '#b33b32' : x.status === 'running' ? '#267448' : '#59625d'}">${esc(label(x.status))}${esc(progress)}</div><div style="margin-top:2px;color:#7a827d;font:11px/1.35 Arial;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${esc(x.message || 'Bekliyor')}${esc(duration)}</div></div></div>`;
    }).join('');
    if (log.finalMessage) list.innerHTML += `<div style="margin-top:9px;padding:8px 10px;background:#f7f9f7;border-radius:8px;color:${log.status === 'failed' ? '#b33b32' : '#59625d'};font:11px/1.4 Arial">${esc(log.finalMessage)}</div>`;
  }
  async function load() { try { const response = await fetch('/api/v5/motor-logs?ts=' + Date.now(), { cache:'no-store' }); if (!response.ok) throw new Error('HTTP ' + response.status); render(await response.json()); } catch (_) { if (running) state.textContent = '⚠️ Motor log bağlantısı alınamadı'; } }
  function start() { running = true; open = true; body.style.display = 'block'; arrow.textContent = '⌃'; renderUnavailable(); load(); if (!timer) timer = setInterval(load, 700); }
  function stop() { running = false; if (timer) { clearInterval(timer); timer = null; } load(); }
  toggle.onclick = function () { open = !open; body.style.display = open ? 'block' : 'none'; arrow.textContent = open ? '⌃' : '⌄'; if (open) load(); };
  refresh.onclick = load;
  renderUnavailable();
  function watchRuntime() {
    const runtime = document.getElementById('runtime'); if (!runtime) return;
    let last = '';
    const check = () => { const text = runtime.textContent || ''; if (text === last) return; last = text; if (/Maç koşulları kaydediliyor|CHPP verileri|analiz başlat|analiz çalışıyor/i.test(text)) start(); if (/analiz tamamlandı|analiz başarısız/i.test(text)) stop(); };
    new MutationObserver(check).observe(runtime, {subtree:true, childList:true, characterData:true}); check();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchRuntime); else watchRuntime();
  const originalFetch = window.fetch;
  window.fetch = async function () {
    const response = await originalFetch.apply(this, arguments);
    try {
      const input = arguments[0], url = typeof input === 'string' ? input : input?.url || '';
      if (String(url).includes('/api/v5/analysis') && response.ok) {
        response.clone().json().then(data => {
          window.__v5LastAnalysis = data;
          window.dispatchEvent(new CustomEvent('v5:analysis-ready', { detail: data }));
        }).catch(() => {});
        setTimeout(load, 50);
      }
    } catch (_) {}
    return response;
  };

  // The original page owns the visual button handlers. We only enrich the
  // clipboard payload after a successful final analysis response.
  document.addEventListener('click', function (event) {
    const target = event.target?.closest?.('#copyOwn,#copyOpp');
    if (!target) return;
    const kind = target.id === 'copyOpp' ? 'opp' : 'own';
    setTimeout(() => copyLineup(kind), 30);
  });
})();
