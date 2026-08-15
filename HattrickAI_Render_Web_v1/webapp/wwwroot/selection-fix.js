(()=>{
  const originalLoadSelectedMatch=window.loadSelectedMatch;

  window.loadSelectedMatch=async function(index=selectedRecentIndex||0){
    const previousView=currentView;
    const previousIndex=selectedRecentIndex;
    await originalLoadSelectedMatch(index);

    // app.js handles the HTTP error internally and leaves currentView untouched.
    // Never let a failed historical request erase the already visible match data.
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
        const message=document.querySelector('#explanation')?.textContent||'Geçmiş maç bilgisi alınamadı.';
        bar.innerHTML=`<b>Maç seçilemedi:</b> ${escapeHtml(message)} <span>Mevcut analiz korunuyor.</span>`;
      }
    }
  };
})();
