(()=>{
  const originalLoadSelectedMatch=window.loadSelectedMatch;
  const originalSelectRecent=window.selectRecent;

  window.loadSelectedMatch=async function(index=window.selectedRecentIndex||0){
    if(!window.currentMatchId && typeof currentMatchId==='undefined') return;
    const previousView=window.currentView;
    try{
      return await originalLoadSelectedMatch(index);
    }catch(e){
      console.error('Historical match selection failed',e);
      if(previousView){
        window.currentView=previousView;
        if(previousView.recentMatches) renderRecent(previousView.recentMatches);
      }
      const bar=document.querySelector('#recentSelection');
      if(bar) bar.innerHTML=`<b>Maç seçilemedi:</b> ${escapeHtml(e.message||'Geçmiş maç bilgisi alınamadı.')} <span>Mevcut analiz korunuyor.</span>`;
    }
  };

  window.selectRecent=async function(i){
    const previousView=window.currentView;
    const previousIndex=window.selectedRecentIndex||0;
    try{
      window.selectedRecentIndex=i;
      if(previousView?.recentMatches) renderRecent(previousView.recentMatches);
      await window.loadSelectedMatch(i);
    }catch(e){
      window.selectedRecentIndex=previousIndex;
      if(previousView){
        window.currentView=previousView;
        if(previousView.recentMatches) renderRecent(previousView.recentMatches);
      }
      const bar=document.querySelector('#recentSelection');
      if(bar) bar.innerHTML=`<b>Maç seçilemedi:</b> ${escapeHtml(e.message||'Geçmiş maç bilgisi alınamadı.')} <span>Mevcut analiz korunuyor.</span>`;
    }
  };
})();
