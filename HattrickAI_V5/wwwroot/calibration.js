(function(){
'use strict';
function esc(v){return String(v??'').replace(/[&<>]/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[m]))}
function downloadJson(data){
 const blob=new Blob([JSON.stringify(data,null,2)],{type:'application/json;charset=utf-8'});
 const url=URL.createObjectURL(blob); const a=document.createElement('a');
 const stamp=new Date().toISOString().replace(/[:.]/g,'-');
 a.href=url; a.download='HattrickAI_V5_Historical_Production_'+stamp+'.json';
 document.body.appendChild(a); a.click(); a.remove(); setTimeout(()=>URL.revokeObjectURL(url),1000);
}
function buildTestDataset(d){
 const sourceRows=Array.isArray(d.rows)?d.rows:[];
 const samples=sourceRows.filter(x=>!x.error&&Number.isFinite(Number(x.ownSectorChances))&&Number.isFinite(Number(x.opponentSectorChances))).map(x=>({
   matchId:String(x.matchId), isHome:!!x.isHome,
   midfieldShare:Number(x.ownPossessionPercent)/100,
   observedTotalRegularChances:(Number(x.ownSectorChances)||0)+(Number(x.opponentSectorChances)||0),
   observedOwnRegularChances:Number(x.ownSectorChances)||0,
   observedOpponentRegularChances:Number(x.opponentSectorChances)||0,
   observedOwnLeftChances:Number.isFinite(Number(x.ownLeftChances))?Number(x.ownLeftChances):null,
   observedOwnCentreChances:Number.isFinite(Number(x.ownCentreChances))?Number(x.ownCentreChances):null,
   observedOwnRightChances:Number.isFinite(Number(x.ownRightChances))?Number(x.ownRightChances):null,
   observedOwnSetPieceChances:null,
   ownTactic:x.isHome?x.homeTactic:x.awayTactic,
   opponentTactic:x.isHome?x.awayTactic:x.homeTactic,
   ownTacticSkill:x.isHome?x.homeTacticSkill:x.awayTacticSkill,
   opponentTacticSkill:x.isHome?x.awayTacticSkill:x.homeTacticSkill,
   ownGoals:x.isHome?x.homeGoals:x.awayGoals,
   opponentGoals:x.isHome?x.awayGoals:x.homeGoals
 }));
 return {
  schema:'hattrickai-v5-historical-production-v1',
  exportedAt:new Date().toISOString(), phase:'D', source:'CHPP',
  purpose:'Historical multi-match production acceptance corpus for V5/M8 observation and validation',
  productionCoefficientsChanged:false,
  sampleCount:samples.length,
  minimumAcceptanceMatches:250,
  sourceSummary:{sampleCount:d.sampleCount,detailsFetched:d.detailsFetched,failedDetails:d.failedDetails,chanceSamples:d.chanceSamples,archiveRawMatchCount:d.archiveRawMatchCount,archiveUniqueMatchCount:d.archiveUniqueMatchCount,archiveWindowCount:d.archiveWindowCount,archiveWindowDays:d.archiveWindowDays,meanOwnPossessionPercent:d.meanOwnPossessionPercent,totalObservedSectorChances:d.totalObservedSectorChances},
  rows:d.rows,
  samples
 };
}
function init(){
 if(document.getElementById('m8CalibrationBox')) return;
 const host=document.querySelector('.analysis'); if(!host) return;
 const box=document.createElement('div'); box.id='m8CalibrationBox'; box.style.cssText='margin-top:12px;border:1px solid #dbe3de;border-radius:12px;background:#f7faf8;overflow:hidden';
 box.innerHTML='<button id="m8CalibrationBtn" type="button" style="width:100%;border:0;background:#f7faf8;padding:13px 14px;text-align:left;font:800 13px Arial;color:#27322d;cursor:pointer">📊 260 GEÇMİŞ MAÇI ÇEK + PRODUCTION JSON İNDİR</button><div id="m8CalibrationState" style="display:none;padding:10px 14px;border-top:1px solid #dbe3de;color:#66716b;font:11px/1.45 Arial"></div>';
 host.appendChild(box);
 const btn=box.querySelector('#m8CalibrationBtn'),state=box.querySelector('#m8CalibrationState');
 btn.onclick=async()=>{
  btn.disabled=true; state.style.display='block';
  state.textContent='CHPP 12 aylık arşiv taranıyor; 260 maçın matchdetails kayıtları sırayla çekiliyor. CHPP yükünü korumak için istekler arasında 5 sn bekleniyor…';
  try{
   const r=await fetch('/api/v5/reference-match?calibration=1&limit=260&ts='+Date.now(),{cache:'no-store'});
   const d=await r.json().catch(()=>({}));
   if(!r.ok)throw Error(d.detail||d.message||('HTTP '+r.status));
   const dataset=buildTestDataset(d); downloadJson(dataset);
   const ready=dataset.sampleCount>=250&&Number(d.detailsFetched)>=250&&Number(d.failedDetails||0)===0;
   state.innerHTML='<b>HISTORICAL PRODUCTION DATA</b><br>Arşiv: '+esc(d.archiveUniqueMatchCount)+' benzersiz maç • Pencere: '+esc(d.archiveWindowCount)+' × '+esc(d.archiveWindowDays)+' gün<br>İstenen: '+esc(d.requestedLimit)+' • Detay: '+esc(d.detailsFetched)+' • Hatalı: '+esc(d.failedDetails||0)+'<br>Geçerli örnek: '+esc(dataset.sampleCount)+' • Toplam sektör şansı: '+esc(d.totalObservedSectorChances)+'<br><span style="color:#267448">'+(ready?'250+ kabul eşiği sağlandı.':'Veri çekildi ancak 250+ kabul eşiği henüz sağlanmadı.')+' JSON indirildi; üretim katsayıları değiştirilmedi.</span>';
  }catch(e){state.textContent='❌ Veri çekilemedi: '+e.message}
  finally{btn.disabled=false}
 };
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();