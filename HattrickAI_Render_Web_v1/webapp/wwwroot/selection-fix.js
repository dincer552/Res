(()=>{
  // Version is rendered only by the server from /VERSION.
  // Do not overwrite .app-version on the client; this keeps one authoritative source.

  // v23.01.23: per-match data state. A check means THIS match's detailed
  // data has actually been loaded in this browser session. Do not infer
  // "loaded" merely because historical ratings exist in the list payload.
  let recentLoadingIndex=null;

  const isRecentLoaded=(match,index)=>{
    const key=currentMatchId==null?null:`${currentMatchId}:${Math.max(0,Number(index)||0)}`;
    if(key && fixtureViewCache.has(key)) return true;
    return !!match?.dataLoaded;
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
      }else if(isRecentLoaded(matches[i],i)){
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

  const decorateFixtureCards=()=>{
    document.querySelectorAll('#fixturesList .fixture-card').forEach(card=>{
      card.classList.remove('recent-data-loaded','recent-data-loading');
      card.querySelector('.recent-data-state')?.remove();
      const onclick=card.getAttribute('onclick')||'';
      const match=onclick.match(/selectFixture\((\d+)\)/);
      const id=match?Number(match[1]):null;
      if(id!==null && fixtureViewCache.has(cacheKey(id,selectedRecentIndex))){
        card.classList.add('recent-data-loaded');
        const el=document.createElement('span');
        el.className='recent-data-state';
        el.setAttribute('aria-label','Maç verileri alındı');
        el.title='Maç verileri alındı';
        el.textContent='✓';
        card.appendChild(el);
      }
    });
  };

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
      decorateFixtureCards();
      return;
    }

    await originalLoadSelectedMatch(normalized);
    if(currentView?.fixture?.matchId===currentMatchId && Number(currentView.selectedRecentIndex??normalized)===normalized){
      fixtureViewCache.set(key,currentView);
      showCacheInfo(currentView);
    }
    recentLoadingIndex=null;
    decorateRecentCards();
    decorateFixtureCards();
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
    decorateFixtureCards();
  };

  const recentRefresh=document.querySelector('#recent .icon-btn');
  if(recentRefresh) recentRefresh.setAttribute('onclick','loadSelectedMatch(undefined,true)');

  // Catch the initial async fixture/history render, which may finish after this script loads.
  setTimeout(()=>{decorateRecentCards();decorateFixtureCards()},0);
})();
