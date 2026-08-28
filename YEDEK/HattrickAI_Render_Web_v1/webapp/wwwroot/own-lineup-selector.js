(()=>{
  // The automatic league analysis now owns the recommended lineup.
  // This legacy selector must not clear or mutate currentView after render.
  let selectedMode='best';

  window.setOwnAnalysisMode=mode=>{
    selectedMode=mode==='cup'?'cup':'best';
  };
})();