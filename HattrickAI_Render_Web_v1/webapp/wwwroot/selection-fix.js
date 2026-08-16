(()=>{
  const APP_VERSION='v23.01.10';
  document.querySelectorAll('.app-version').forEach(el=>el.textContent=APP_VERSION);
  const footer=document.querySelector('footer');
  if(footer) footer.textContent=footer.textContent.replace(/v23\.01\.\d+/,'v23.01.10');

  const originalLoadSelectedMatch=window.loadSelectedMatch;

  window.loadSelectedMatch=async function(index=selectedRecentIndex||0){
    const previousView=currentView;
    const previousIndex=selectedRecentIndex;
    await originalLoadSelectedMatch(index);

    // app.js handles historical-request errors internally. Restore the previous
    // view and recent-match list instead of replacing the cards with an error.
    if(previousView && currentView===previousView){
      selectedRecentIndex=previousIndex;
      if(previousView.recentMatches) renderRecent(previousView.recentMatches);
    }
  };

  window.selectRecent=async function(i){
    const previousView=currentView;
    const previousIndex=selectedRecentIndex;
    selectedRecentIndex=i;
    if(previousView?.recentMatches) renderRecent(previousView.recentMatches);

    await window.loadSelectedMatch(i);

    if(previousView && currentView===previousView){
      selectedRecentIndex=previousIndex;
      renderRecent(previousView.recentMatches||[]);

      const bar=document.querySelector('#recentSelection');
      if(bar){
        bar.innerHTML='<b>Maç verisi alınamadı.</b> Mevcut maç bilgileri korunuyor.';
      }
      const explanation=document.querySelector('#explanation');
      if(explanation){
        explanation.textContent='Rakip maç verisi alınamadı. Mevcut analiz korunuyor.';
      }
    }
  };
})();
