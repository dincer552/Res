(()=>{
  const APP_VERSION='v23.01.16';
  document.querySelectorAll('.app-version').forEach(el=>el.textContent=APP_VERSION);
  const footer=document.querySelector('footer');
  if(footer) footer.textContent=footer.textContent.replace(/v23\.01\.\d+/,'v23.01.16');

  // v23.01.16: keep the already loaded fixture/history response in the browser
  // for the current session. Re-selecting the same fixture or historical match
  // must not even call /api/fixture-view again. PostgreSQL remains the persistent
  // cache across reloads/deploys; this is only the zero-request UI layer.
  const fixtureViewCache=new Map();
  const cacheKey=(matchId,index)=>`${matchId}:${Math.max(0,Number(index)||0)}`;

  const showCacheInfo=(view)=>{
    if(!view?.cache)return;
    const bar=document.querySelector('#recentSelection');
    if(!bar)return;
    const c=view.cache;
    const source=c.history==='POSTGRES_CACHE'?'PostgreSQL önbellek':(c.history||'CHPP');
    const lineup=c.lineup==='POSTGRES_CACHE'?'DB kadro':(c.lineup||'CHPP');
    const analysis=c.analysis==='COMPUTED_AND_CACHED'?'DB HO sonucu':(c.analysis||'HO hesaplandı');
    bar.dataset.cacheInfo=`${source} • ${lineup} • ${analysis}`;
    const old=bar.querySelector('.cache-status');
    if(old)old.remove();
    bar.insertAdjacentHTML('beforeend',`<small class="cache-status">${source} • ${lineup} • ${analysis}</small>`);
  };

  const originalLoadSelectedMatch=window.loadSelectedMatch;
  window.loadSelectedMatch=async function(index=selectedRecentIndex||0,forceRefresh=false){
    const normalized=Math.max(0,Number(index)||0);
    const key=cacheKey(currentMatchId,normalized);

    if(!forceRefresh && fixtureViewCache.has(key)){
      currentView=fixtureViewCache.get(key);
      selectedRecentIndex=Number(currentView.selectedRecentIndex??normalized);
      renderMatch(currentView);
      showCacheInfo(currentView);
      return;
    }

    await originalLoadSelectedMatch(normalized);
    if(currentView?.fixture?.matchId===currentMatchId && Number(currentView.selectedRecentIndex??normalized)===normalized){
      fixtureViewCache.set(key,currentView);
      showCacheInfo(currentView);
    }
  };

  window.selectRecent=async function(i){
    const previousView=currentView;
    const previousIndex=selectedRecentIndex;
    selectedRecentIndex=i;
    if(previousView?.recentMatches) renderRecent(previousView.recentMatches);
    await window.loadSelectedMatch(i);
    if(previousView && currentView===previousView && previousIndex!==i){
      selectedRecentIndex=previousIndex;
      renderRecent(previousView.recentMatches||[]);
      const bar=document.querySelector('#recentSelection');
      if(bar) bar.innerHTML='<b>Maç verisi alınamadı.</b> Mevcut maç bilgileri korunuyor.';
      const explanation=document.querySelector('#explanation');
      if(explanation) explanation.textContent='Rakip maç verisi alınamadı. Mevcut analiz korunuyor.';
    }
  };

  // The visible refresh action is the one place allowed to bypass the browser cache.
  const recentRefresh=document.querySelector('#recent .icon-btn');
  if(recentRefresh) recentRefresh.setAttribute('onclick','loadSelectedMatch(undefined,true)');
})();
