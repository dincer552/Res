(function () {
  'use strict';

  const existing = document.getElementById('v5MotorLogBox');
  if (existing) return;

  const motorDefs = [
    ['M3', 'Player Analysis', 'Oyuncu uygunlukları, pozisyon skorları, birincil/ikincil pozisyonlar'],
    ['M4', 'Formation Candidate', 'Yasal dizilişler, yapılabilirlik ve yapısal skorlar'],
    ['M5', 'Position / XI', 'XI adayları, oyuncu-slot atamaları, suitability ve structural skor'],
    ['M6', 'Global Optimization', 'Baseline, iterasyonlar, frontier, beam, değişim deltaları ve convergence'],
    ['M7', 'Regional Ratings', 'Aday/diziliş/davranış, 7 bölgesel rating, modifier ve confidence'],
    ['M7.2', 'Advanced Tactical', 'Taktik seviyesi, beceri toplamları, şans dağılımı ve aktif taktik profili'],
    ['M8', 'Chance / Matchup', 'Rakip karşılaştırması, MID ve üç hücum-savunma eşleşmesi, structural chance'],
    ['M9', 'Match Prediction', 'Beklenen goller, kazanma/beraberlik/kaybetme olasılıkları ve normalizasyon'],
    ['M10', 'Final Decision', 'Aday skorları, ağırlıklar, composite sıralama ve seçilen plan']
  ];

  const box = document.createElement('section');
  box.id = 'v5MotorLogBox';
  box.style.cssText = 'margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';

  box.innerHTML = `
    <button id="v5MotorLogToggle" type="button" style="width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer">
      <span>🧠 V5 Motor Logları • M3 → M10</span>
      <span id="v5MotorLogArrow">⌄</span>
    </button>
    <div id="v5MotorLogBody" style="display:none;border-top:1px solid #e5ebe7">
      <div style="padding:9px 12px;display:flex;justify-content:space-between;align-items:center;color:#747c76;font:11px Arial">
        <span id="v5MotorLogState">Motor log akışı hazır</span>
        <button id="v5MotorLogRefresh" type="button" style="border:1px solid #d5ddd8;background:#f7f9f7;border-radius:7px;padding:5px 8px;cursor:pointer">↻ Yenile</button>
      </div>
      <div id="v5MotorLogList" style="padding:0 12px 12px"></div>
    </div>`;

  const deployBox = document.getElementById('deployLogBox');
  if (deployBox && deployBox.parentNode) {
    deployBox.parentNode.insertBefore(box, deployBox);
  } else {
    const main = document.querySelector('main.page') || document.querySelector('main');
    if (main) main.appendChild(box);
    else document.body.appendChild(box);
  }

  const toggle = document.getElementById('v5MotorLogToggle');
  const body = document.getElementById('v5MotorLogBody');
  const arrow = document.getElementById('v5MotorLogArrow');
  const list = document.getElementById('v5MotorLogList');
  const state = document.getElementById('v5MotorLogState');
  const refresh = document.getElementById('v5MotorLogRefresh');
  let open = false;

  function esc(value) {
    return String(value ?? '').replace(/[&<>\"']/g, function (m) {
      return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '\"': '&quot;', "'": '&#039;' })[m];
    });
  }

  function render() {
    list.innerHTML = motorDefs.map(function (m) {
      return '<div style="border-bottom:1px solid #edf0ee;padding:10px 2px">' +
        '<div style="display:flex;align-items:center;gap:8px">' +
        '<span style="min-width:42px;padding:4px 6px;border-radius:6px;background:#e5f1e9;color:#1d7043;font:900 10px Arial;text-align:center">' + esc(m[0]) + '</span>' +
        '<b style="font:800 12px Arial;color:#27322d">' + esc(m[1]) + '</b>' +
        '</div>' +
        '<div style="margin:5px 0 0 50px;color:#707872;font:11px/1.45 Arial">' + esc(m[2]) + '</div>' +
      '</div>';
    }).join('');
    state.textContent = 'M3 → M10 log şeması hazır • ' + motorDefs.length + ' motor';
  }

  toggle.onclick = function () {
    open = !open;
    body.style.display = open ? 'block' : 'none';
    arrow.textContent = open ? '⌃' : '⌄';
    if (open) render();
  };

  refresh.onclick = render;
  render();
})();
