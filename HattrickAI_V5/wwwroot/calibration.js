(function(){
'use strict';
function esc(v){return String(v??'').replace(/[&<>]/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[m]))}
function downloadJson(data){
 const blob=new Blob([JSON.stringify(data,null,2)],{type:'application/json;charset=utf-8'});
 const url=URL.createObjectURL(blob); const a=document.createElement('a');
 const stamp=new Date().toISOString().replace(/[:.]/g,'-');
 a.href=url; a.download='HattrickAI_V5_M8_PhaseD_Calibration_'+stamp+'.json';
 document.body.appendChild(a); a.click(); a.remove(); setTimeout(()=>URL.revokeObjectURL(url),1000);
}
function buildTestDataset(d){
 const sourceRows=Array.isArray(d.rows)?d.rows:[];
 const samples=sourceRows.filter(x=>!x.error&&Number.isFinite(Number(x.ownSectorChances))&&Number.isFinite(Number(x.opponentSectorChances))).map(x=>{
  const own=Number(x.ownSectorChances)||0, opp=Number(x.opponentSectorChances)||0;
  return {
   matchId:String(x.matchId),
   isHome:!!x.isHome,
   midfieldShare:Number(x.ownPossessionPercent)/100,
   observedTotalRegularChances:own+opp,
   observedOwnRegularChances:own,
   observedOpponentRegularChances:opp,
   observedOwnLeftChances:null,
   observedOwnCentreChances:null,
   observedOwnRightChances:null,
   observedOwnSetPieceChances:null,
   ownTactic:x.isHome?x.homeTactic:x.awayTactic,
   opponentTactic:x.isHome?x.awayTactic:x.homeTactic,
   ownGoals:x.isHome?x.homeGoals:x.awayGoals,
   opponentGoals:x.isHome?x.awayGoals:x.homeGoals
  };
 });
 return {
  schema:'hattrickai-v5-m8-phase-d-calibration-v1',
  exportedAt:new Date().toISOString(),
  phase:'D',
  purpose:'Offline calibration/test dataset for M8 historical chance volume',
  productionCoefficientsChanged:false,
  sampleCount:samples.length,
  sourceSummary:{sampleCount:d.sampleCount,detailsFetched:d.detailsFetched,chanceSamples:d.chanceSamples,meanOwnPossessionPercent:d.meanOwnPossessionPercent,totalObservedSectorChances:d.totalObservedSectorChances},
  samples,
  sourceRows
 };
}
function init(){
 if(document.getElementById('m8CalibrationBox')) return;
 const host=document.querySelector('.analysis'); if(!host) return;
 const box=document.createElement('div'); box.id='m8CalibrationBox'; box.style.cssText='margin-top:12px;border:1px solid #dbe3de;border-radius:12px;background:#f7faf8;overflow:hidden';
 box.innerHTML='<button id="m8CalibrationBtn" type="button" style="width:100%;border:0;background:#f7faf8;padding:13px 14px;text-align:left;font:800 13px Arial;color:#27322d;cursor:pointer">📊 GEÇMİŞ MAÇ VERİLERİNİ ÇEK + JSON İNDİR</button><div id="m8CalibrationState" style="display:none;padding:10px 14px;border-top:1px solid #dbe3de;color:#66716b;font:11px/1.45 Arial"></div>';
 host.appendChild(box);
 const btn=box.querySelector('#m8CalibrationBtn'),state=box.querySelector('#m8CalibrationState');
 btn.onclick=async()=>{btn.disabled=true;state.style.display='block';state.textContent='CHPP geçmiş maçları ve M8 şans verileri çekiliyor…';try{const r=await fetch('/api/v5/reference-match?calibration=1&limit=50&ts='+Date.now(),{cache:'no-store'});const d=await r.json().catch(()=>({}));if(!r.ok)throw Error(d.detail||d.message||('HTTP '+r.status));const dataset=buildTestDataset(d);downloadJson(dataset);state.innerHTML='<b>PHASE D VERİ TOPLAMA</b><br>Maç: '+esc(d.sampleCount)+' • Detay: '+esc(d.detailsFetched)+' • Test örneği: '+esc(dataset.sampleCount)+'<br>Ortalama kendi topa sahip olma: '+esc(d.meanOwnPossessionPercent)+'%<br>Toplam sektör şansı: '+esc(d.totalObservedSectorChances)+'<br><span style="color:#267448">Veri çekildi ve test JSON otomatik indirildi. Üretim katsayıları değiştirilmedi.</span>';}catch(e){state.textContent='❌ Veri çekilemedi: '+e.message}finally{btn.disabled=false}};
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();