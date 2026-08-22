(()=>{
const V='20260822-league-auto-02';
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
const num=(v,d=0)=>{const n=Number(v);return Number.isFinite(n)?n:d};
function hideOldControls(){
  ['#fixtures','#recent','#integratedPlanButton','#integratedPlanningInlineHost','#ipwBackdrop','#ipwPreloadStatus'].forEach(sel=>document.querySelectorAll(sel).forEach(el=>{el.style.display='none';el.setAttribute('aria-hidden','true')}));
  document.querySelectorAll('nav a[href="#recent"]').forEach(a=>{a.style.display='none'});
}
function ensurePanel(){
  let panel=document.getElementById('leagueAutoPanel');
  if(panel)return panel;
  const grid=document.querySelector('.lineup-grid');
  if(!grid)return null;
  panel=document.createElement('section');
  panel.id='leagueAutoPanel';
  panel.className='panel league-auto-panel';
  panel.innerHTML=`<div class="league-auto-head"><div><div class="league-auto-kicker">BİZİM TAKIM</div><div class="league-auto-title">Otomatik Lig Kadrosu</div></div><span class="league-auto-badge">SON LİG MAÇINA GÖRE</span></div><div class="league-auto-grid"><div class="league-auto-source" id="leagueAutoSource"><small>Rakip baz maçı</small><strong>Veriler bekleniyor…</strong><span>Rakibin en son tamamlanan lig maçı kullanılacak.</span></div><div class="league-auto-summary"><small>Kadro yaklaşımı</small><strong>Mevcut oyunculardan en iyi 11</strong><div class="league-auto-note">Formasyon veya rakip maçı seçimi yok. Sistem doğrudan son lig maçını baz alır ve en güçlü yerleşimi önerir.</div></div></div><div class="league-auto-stats"><div class="league-auto-stat"><span>FORMASYON</span><b id="leagueAutoFormation">—</b></div><div class="league-auto-stat"><span>MF</span><b id="leagueAutoMF">—</b></div><div class="league-auto-stat"><span>DEF / ATT</span><b id="leagueAutoDefAtt">—</b></div></div><div class="league-auto-progress"><i></i></div><div class="league-auto-status" id="leagueAutoStatus">Rakip ve kadro verileri hazırlanıyor…</div>`;
  grid.parentNode.insertBefore(panel,grid);
  return panel;
}
function recentIsLeague(m){return Number(m?.fixture?.matchType)===1||String(m?.fixture?.matchType||'').toLowerCase()==='league'}
function latestLeagueIndex(view){
  const ms=view?.recentMatches||[];
  let index=ms.findIndex(recentIsLeague);
  if(index<0 && ms.length)index=0;
  return index;
}
function renderSource(view,index){
  const panel=ensurePanel();if(!panel)return;
  const m=view?.recentMatches?.[index];const f=m?.fixture||{};const r=m?.opponent?.actualMatchRatings||m?.opponent?.ratings||{};
  const box=document.getElementById('leagueAutoSource');if(!box)return;
  if(!m){box.innerHTML='<small>Rakip baz maçı</small><strong>Bulunamadı</strong><span>Rakibin son lig maçı alınamadı.</span>';return}
  box.innerHTML=`<small>Rakibin son lig maçı</small><strong>${esc(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName)}</strong><span>${new Date(f.matchDate).toLocaleString('tr-TR',{day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'})} • MF ${num(r.midfield??r.Midfield).toFixed(2)} • DEF ${((num(r.leftDefence??r.LeftDefence)+num(r.centralDefence??r.CentralDefence)+num(r.rightDefence??r.RightDefence))/3).toFixed(2)} • ATT ${((num(r.leftAttack??r.LeftAttack)+num(r.centralAttack??r.CentralAttack)+num(r.rightAttack??r.RightAttack))/3).toFixed(2)}</span>`;
}
function renderResult(view,index){
  const panel=ensurePanel();if(!panel)return;
  const result=view?.ownLineup?.players||[];const ratings=view?.ownRatings||view?.recommendation?.ratings||{};
  if(result.length!==11){panel.classList.add('error');document.getElementById('leagueAutoStatus').textContent='11 oyunculuk kadro oluşturulamadı.';return}
  panel.classList.add('ready');
  document.getElementById('leagueAutoFormation').textContent=view?.formation||view?.ownLineup?.formation||'—';
  document.getElementById('leagueAutoMF').textContent=num(ratings.midfield??ratings.Midfield).toFixed(2);
  const def=(num(ratings.leftDefence??ratings.LeftDefence)+num(ratings.centralDefence??ratings.CentralDefence)+num(ratings.rightDefence??ratings.RightDefence))/3;
  const att=(num(ratings.leftAttack??ratings.LeftAttack)+num(ratings.centralAttack??ratings.CentralAttack)+num(ratings.rightAttack??ratings.RightAttack))/3;
  document.getElementById('leagueAutoDefAtt').textContent=`${def.toFixed(2)} / ${att.toFixed(2)}`;
  document.getElementById('leagueAutoStatus').textContent=`Tamamlandı • ${result.length} oyuncu • ${view?.tactic?.tacticName||'Normal'}${view?.tactic?.tacticLevel?` Lv.${view.tactic.tacticLevel}`:''}`;
  if(typeof renderPitch==='function')renderPitch('#ownPitch',result,false);
  const title=document.getElementById('ownLineupTitle');if(title)title.textContent='Lig Kadrosu';
  const formation=document.getElementById('ownFormation');if(formation)formation.textContent=view?.formation||'—';
  const strip=document.getElementById('ownRatingStrip');if(strip&&typeof renderRatingSummary==='function')strip.innerHTML=renderRatingSummary(ratings);
  const explanation=document.getElementById('explanation');if(explanation)explanation.textContent=view?.recommendation?.explanation||'Son lig maçına göre en iyi 11 oluşturuldu.';
  const selected=document.getElementById('selectedOpponentMatch');
  if(selected){const f=view?.recentMatches?.[index]?.fixture||{};selected.innerHTML=`<div class="selected-opponent-main"><div><strong>Lig baz maçı: ${esc(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName)}</strong><small>Bu maç rakibin son lig maçı olarak otomatik seçildi.</small></div></div>`}
}
async function autoPlan(){
  const panel=ensurePanel();if(!panel)return;
  hideOldControls();
  const status=document.getElementById('leagueAutoStatus');
  for(let attempt=0;attempt<40;attempt++){
    if(typeof currentView!=='undefined'&&currentView?.recentMatches?.length)break;
    status.textContent='Rakibin maç geçmişi hazırlanıyor…';
    await sleep(350);
    hideOldControls();
  }
  const view=typeof currentView!=='undefined'?currentView:null;
  if(!view?.recentMatches?.length){status.textContent='Rakip geçmişi alınamadı.';panel.classList.add('error');return}
  const index=latestLeagueIndex(view);renderSource(view,index);
  if(index<0){status.textContent='Son lig maçı bulunamadı.';panel.classList.add('error');return}
  try{
    if(typeof selectedRecentIndex!=='undefined'&&Number(selectedRecentIndex)!==index&&typeof loadSelectedMatch==='function'){
      status.textContent='Son lig maçı baz alınarak kadro yeniden hesaplanıyor…';
      await loadSelectedMatch(index);
    }
    hideOldControls();
    const finalView=typeof currentView!=='undefined'?currentView:view;
    renderSource(finalView,index);
    renderResult(finalView,index);
  }catch(e){
    panel.classList.add('error');status.textContent=`Lig kadrosu hesaplanamadı: ${e.message||'Bilinmeyen hata'}`;
  }
}
function boot(){
  const link=document.createElement('link');link.rel='stylesheet';link.href=`/league-auto-lineup.css?v=${V}`;document.head.appendChild(link);
  ensurePanel();hideOldControls();autoPlan();
  setInterval(()=>{hideOldControls()},600);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',boot);else boot();
})();
