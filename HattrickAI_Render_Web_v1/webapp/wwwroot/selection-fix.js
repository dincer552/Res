(()=>{
  const APP_VERSION='v23.01.17';
  document.querySelectorAll('.app-version').forEach(el=>el.textContent=APP_VERSION);
  const footer=document.querySelector('footer');
  if(footer) footer.textContent=footer.textContent.replace(/v23\.01\.\d+/,'v23.01.17');

  // v23.01.17: show a small per-match data state without changing the
  // rating/analysis engine. A check means the historical match data needed
  // by the UI is already present; a spinner is shown only while that match
  // is being fetched/selected.
  let recentLoadingIndex=null;

  const hasHistoricalData=m=>{
    const r=m?.opponent?.actualMatchRatings||m?.opponent?.ratings||m?.opponent?.Ratings;
    return !!r && Object.keys(r).length>0;
  };

  const decorateRecentCards=()=>{
    const root=document.querySelector('#recentMatches');
    if(!root)return;
    const matches=currentView?.recentMatches||[];
    [...root.children].forEach((card,i)=>{
      if(!(card instanceof HTMLElement))return;
      card.classList.remove('recent-data-loading','recent-data-loaded');
      card.querySelector('.recent-data-state')?.remove();
      let state='';
      if(recentLoadingIndex===i){
        state='loading';
        card.classList.add('recent-data-loading');
      }else if(hasHistoricalData(matches[i])){
        state='loaded';
        card.classList.add('recent-data-loaded');
      }
      if(state){
        const el=document.createElement('span');
        el.className='recent-data-state';
        el.setAttribute('aria-label',state==='loaded'?'Veriler alındı':'Veriler yükleniyor');
        el.title=state==='loaded'?'Maç verileri alındı':'Maç verileri yükleniyor';
        el.innerHTML=state==='loaded'?'✓':'<i></i>';
        card.appendChild(el);
      }
    });
  };

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
    recentLoadingIndex=normalized;
    decorateRecentCards();

    if(!forceRefresh && fixtureViewCache.has(key)){
      currentView=fixtureViewCache.get(key);
      selectedRecentIndex=Number(currentView.selectedRecentIndex??normalized);
      recentLoadingIndex=null;
      renderMatch(currentView);
      showCacheInfo(currentView);
      decorateRecentCards();
      return;
    }

    await originalLoadSelectedMatch(normalized);
    if(currentView?.fixture?.matchId===currentMatchId && Number(currentView.selectedRecentIndex??normalized)===normalized){
      fixtureViewCache.set(key,currentView);
      showCacheInfo(currentView);
    }
    recentLoadingIndex=null;
    decorateRecentCards();
  };

  const originalRenderRecent=window.renderRecent;
  if(typeof originalRenderRecent==='function'){
    window.renderRecent=function(matches){
      originalRenderRecent(matches);
      decorateRecentCards();
    };
  }

  window.selectRecent=async function(i){
    const previousView=currentView;
    const previousIndex=selectedRecentIndex;
    selectedRecentIndex=i;
    recentLoadingIndex=i;
    if(previousView?.recentMatches) renderRecent(previousView.recentMatches);
    decorateRecentCards();
    await window.loadSelectedMatch(i);
    if(previousView && currentView===previousView && previousIndex!==i){
      selectedRecentIndex=previousIndex;
      recentLoadingIndex=null;
      renderRecent(previousView.recentMatches||[]);
      const bar=document.querySelector('#recentSelection');
      if(bar) bar.innerHTML='<b>Maç verisi alınamadı.</b> Mevcut maç bilgileri korunuyor.';
      const explanation=document.querySelector('#explanation');
      if(explanation) explanation.textContent='Rakip maç verisi alınamadı. Mevcut analiz korunuyor.';
    }
    decorateRecentCards();
  };

  const recentRefresh=document.querySelector('#recent .icon-btn');
  if(recentRefresh) recentRefresh.setAttribute('onclick','loadSelectedMatch(undefined,true)');

  const recentRoot=document.querySelector('#recentMatches');
  if(recentRoot){
    new MutationObserver(()=>decorateRecentCards()).observe(recentRoot,{childList:true});
    decorateRecentCards();
  }
})();
