(function(){
'use strict';
function esc(v){return String(v??'').replace(/[&<>]/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[m]))}
function init(){
 if(document.getElementById('m8CalibrationBox')) return;
 const host=document.querySelector('.analysis'); if(!host) return;
 const box=document.createElement('div'); box.id='m8CalibrationBox'; box.style.cssText='margin-top:12px;border:1px solid #dbe3de;border-radius:12px;background:#f7faf8;overflow:hidden';
 box.innerHTML='<button id="m8CalibrationBtn" type="button" style="width:100%;border:0;background:#f7faf8;padding:13px 14px;text-align:left;font:800 13px Arial;color:#27322d;cursor:pointer">📊 GEÇMİŞ MAÇ VERİLERİNİ ÇEK</button><div id="m8CalibrationState" style="display:none;padding:10px 14px;border-top:1px solid #dbe3de;color:#66716b;font:11px/1.45 Arial"></div>';
 host.appendChild(box);
 const btn=box.querySelector('#m8CalibrationBtn'),state=box.querySelector('#m8CalibrationState');
 btn.onclick=async()=>{btn.disabled=true;state.style.display='block';state.textContent='CHPP geçmiş maçları ve M8 şans verileri çekiliyor…';try{const r=await fetch('/api/v5/reference-match?calibration=1&limit=40&ts='+Date.now(),{cache:'no-store'});const d=await r.json().catch(()=>({}));if(!r.ok)throw Error(d.detail||d.message||('HTTP '+r.status));state.innerHTML='<b>PHASE D VERİ TOPLAMA</b><br>Maç: '+esc(d.sampleCount)+' • Detay: '+esc(d.detailsFetched)+' • Şans verisi: '+esc(d.chanceSamples)+'<br>Ortalama kendi topa sahip olma: '+esc(d.meanOwnPossessionPercent)+'%<br>Toplam sektör şansı: '+esc(d.totalObservedSectorChances)+'<br><span style="color:#267448">Veri çekildi. Katsayılar henüz değiştirilmedi.</span>';}catch(e){state.textContent='❌ Veri çekilemedi: '+e.message}finally{btn.disabled=false}};
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();