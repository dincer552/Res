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
   opponentGoals:x.isHome?x.opponentGoals:x.homeGoals
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
})();