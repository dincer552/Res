(()=>{
  const style=document.createElement('style');
  style.textContent=`
    .ai-loading-overlay{position:fixed;inset:0;z-index:9999;display:none;align-items:center;justify-content:center;background:rgba(4,14,21,.72);backdrop-filter:blur(7px);padding:20px}
    .ai-loading-overlay.show{display:flex}
    .ai-loading-card{width:min(420px,92vw);background:linear-gradient(145deg,#0d2431,#071821);border:1px solid #21485c;border-radius:16px;padding:22px;box-shadow:0 22px 70px rgba(0,0,0,.45)}
    .ai-loading-title{font-weight:900;font-size:16px;color:#f5f8fa;margin-bottom:5px}
    .ai-loading-text{font-size:11px;color:#8da3b2;margin-bottom:15px}
    .ai-loading-track{height:9px;background:#132f3d;border-radius:99px;overflow:hidden;border:1px solid #1d4558}
    .ai-loading-bar{height:100%;width:7%;border-radius:99px;background:#38e274;transition:width .45s ease;box-shadow:0 0 15px rgba(56,226,116,.35);position:relative;overflow:hidden}
    .ai-loading-bar:after{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(255,255,255,.42),transparent);transform:translateX(-100%);animation:aiLoadingShine 1.1s infinite}
    .ai-loading-percent{margin-top:8px;text-align:right;font-size:10px;font-weight:900;color:#38e274}
    @keyframes aiLoadingShine{to{transform:translateX(100%)}}
  `;
  document.head.appendChild(style);

  const overlay=document.createElement('div');
  overlay.className='ai-loading-overlay';
  overlay.innerHTML='<div class="ai-loading-card"><div class="ai-loading-title">Rakip verileri yükleniyor</div><div class="ai-loading-text" id="aiLoadingText">CHPP geçmiş maç, kadro ve rating bilgileri alınıyor…</div><div class="ai-loading-track"><div class="ai-loading-bar" id="aiLoadingBar"></div></div><div class="ai-loading-percent" id="aiLoadingPercent">7%</div></div>';
  document.body.appendChild(overlay);

  const bar=overlay.querySelector('#aiLoadingBar'),percent=overlay.querySelector('#aiLoadingPercent'),text=overlay.querySelector('#aiLoadingText');
  let timers=[],visible=false,requestId=0;
  const clearTimers=()=>{timers.forEach(clearTimeout);timers=[]};
  const setProgress=(value,message)=>{bar.style.width=value+'%';percent.textContent=value+'%';if(message)text.textContent=message};
  const show=()=>{
    clearTimers();visible=true;requestId++;overlay.classList.add('show');setProgress(7,'CHPP geçmiş maç, kadro ve rating bilgileri alınıyor…');
    timers.push(setTimeout(()=>setProgress(24,'Geçmiş maç listesi hazırlanıyor…'),280));
    timers.push(setTimeout(()=>setProgress(48,'Seçilen maçın 11 oyuncusu getiriliyor…'),750));
    timers.push(setTimeout(()=>setProgress(70,'Oyuncu yetenekleri ve saha yerleşimi eşleştiriliyor…'),1350));
    timers.push(setTimeout(()=>setProgress(86,'HO Engine ratingleri hesaplıyor…'),2100));
    timers.push(setTimeout(()=>setProgress(94,'Analiz tamamlanmak üzere…'),3200));
  };
  const hide=()=>{
    if(!visible)return;
    clearTimers();setProgress(100,'Veriler hazır.');
    const id=++requestId;
    setTimeout(()=>{if(id===requestId){overlay.classList.remove('show');visible=false}},220);
  };

  const originalFetch=window.fetch.bind(window);
  window.fetch=async(...args)=>{
    const input=args[0];
    const url=typeof input==='string'?input:(input?.url||'');
    const tracked=url.includes('/api/fixture-view/')||url.includes('/api/fixtures');
    if(!tracked)return originalFetch(...args);
    show();
    const started=Date.now();
    try{return await originalFetch(...args)}
    finally{
      const wait=Math.max(220-(Date.now()-started),0);
      setTimeout(hide,wait);
    }
  };
})();
