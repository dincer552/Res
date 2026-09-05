(function () {
  'use strict';

  var motors = ['M3','M4','M5','M6','M7','M7.2','M8','M9','M10','M6-B','M11'];
  var names = {
    'M3':'Oyuncu Analizi','M4':'Formasyon Üretimi','M5':'11 Adayı Üretimi','M6':'Global Arama',
    'M7':'Bölgesel Rating','M7.2':'Taktik Senaryo','M8':'Şans & Eşleşme','M9':'Maç Tahmini',
    'M10':'Formasyon Kararı','M6-B':'İkinci Arama / İyileştirme','M11':'Final Seçici'
  };

  function ensurePanel() {
    var box = document.getElementById('v5MotorLogBox');
    if (box) {
      box.style.display = 'block';
      var body = document.getElementById('v5MotorLogBody');
      if (body) body.style.display = 'block';
      return box;
    }

    box = document.createElement('section');
    box.id = 'v5MotorLogBox';
    box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1;display:block';
    box.innerHTML = '<div style="padding:14px 16px;color:#27322d;font:800 13px Arial">🧠 V5 Motorlar • M3 → M11</div>' +
      '<div id="v5MotorLogBody" style="display:block;border-top:1px solid #e5ebe7">' +
      '<div style="padding:9px 12px;color:#747c76;font:11px Arial"><span id="v5MotorLogState">Motor durumları yükleniyor…</span></div>' +
      '<div id="v5MotorLogList" style="padding:0 12px"></div></div>';

    var deployBox = document.getElementById('deployLogBox');
    if (deployBox && deployBox.parentNode) deployBox.parentNode.insertBefore(box, deployBox);
    else (document.querySelector('main.page') || document.querySelector('main') || document.body).appendChild(box);
    return box;
  }

  function render(data) {
    ensurePanel();
    var list = document.getElementById('v5MotorLogList');
    var state = document.getElementById('v5MotorLogState');
    if (!list || !state) return;

    var log = data && data.log;
    var stages = log && Array.isArray(log.stages) ? log.stages : [];
    var by = {};
    stages.forEach(function (x) { by[x.motor] = x; });

    list.innerHTML = motors.map(function (m) {
      var x = by[m] || {};
      var s = x.status || 'pending';
      var icon = s === 'completed' ? '✓' : s === 'failed' ? '✕' : s === 'running' ? '●' : '○';
      var text = s === 'completed' ? 'Tamamlandı' : s === 'failed' ? 'Hata' : s === 'running' ? 'Çalışıyor' : 'Bekliyor';
      var message = x.message ? ' • ' + String(x.message) : '';
      return '<div style="display:flex;align-items:center;gap:10px;border-bottom:1px solid #edf0ee;padding:9px 2px">' +
        '<span style="width:22px;text-align:center;font-size:16px">' + icon + '</span>' +
        '<b style="width:155px;min-width:155px;font:900 12px Arial;color:#27322d">' + names[m] + '</b>' +
        '<span style="color:#8a928d;font:11px Arial">' + text + message + '</span></div>';
    }).join('');

    state.textContent = log ? (log.status === 'completed' ? '🟢 Analiz tamamlandı' : log.status === 'failed' ? '🔴 Analiz hata verdi' : '🟡 Analiz çalışıyor…') : 'Analiz bekleniyor…';
  }

  async function load() {
    try {
      var response = await fetch('/api/v5/motor-logs?ts=' + Date.now(), { cache: 'no-store' });
      if (!response.ok) throw new Error('HTTP ' + response.status);
      render(await response.json());
    } catch (_) {
      ensurePanel();
      var state = document.getElementById('v5MotorLogState');
      if (state) state.textContent = '⚠️ Motor log bağlantısı alınamadı';
    }
  }

  function start() {
    ensurePanel();
    load();
    setInterval(load, 1500);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
