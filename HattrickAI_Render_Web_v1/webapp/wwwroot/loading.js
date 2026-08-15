(()=>{
  const style=document.createElement('style');
  style.textContent=`
    .ai-loading-inline{display:none;margin:10px 0 14px;padding:18px 18px 16px;background:linear-gradient(145deg,#0d2431,#071821);border:1px solid #21485c;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.22)}
    .ai-loading-inline.show{display:block;animation:aiLoadingIn .18s ease-out}
    .ai-loading-title{font-weight:900;font-size:15px;color:#f5f8fa;margin-bottom:5px}
    .ai-loading-text{font-size:11px;color:#8da3b2;margin-bottom:13px}
    .ai-loading-track{height:9px;background:#132f3d;border-radius:99px;overflow:hidden;border:1px solid #1d4558}
    .ai-loading-bar{height:100%;width:7%;border-radius:99px;background:#38e274;transition:width .45s ease;box-shadow:0 0 15px rgba(56,226,116,.35);position:relative;overflow:hidden}
    .ai-loading-bar:after{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(255,255,255,.42),transparent);transform:translateX(-100%);animation:aiLoadingShine 1.1s infinite}
    .ai-loading-percent{margin-top:7px;text-align:right;font-size:10px;font-weight:900;color:#38e274}
    @keyframes aiLoadingShine{to{transform:translateX(100%)}}
    @keyframes aiLoadingIn{from{opacity:0;transform:translateY(-4px)}to{opacity:1;transform:translateY(0)}}
  `;
  document.head.appendChild(style);

  let timers=[],visible=false,currentCard=null,requestId=0;
  const clearTimers=()=>{timers.forEach(clearTimeout);timers=[]};
  const setProgress=(value,message)=>{
    if(!currentCard)return;
    const bar=currentCard.querySelector('#aiLoadingBar');
    const percent=currentCard.querySelector('#aiLoadingPercent');
    const text=currentCard.querySelector('#aiLoadingText');
    if(bar)bar.style.width=value+'%';
    if(percent)percent.textContent=value+'%';
    if(message&&text)text.textContent=message;
  };
  const removeCard=()=>{
    if(currentCard){currentCard.remove();currentCard=null}
    visible=false;
  };
  const createCard=()=>{
    const selected=document.querySelector('.fixture-card.selected');
    if(!selected)return null;
    const old=document.querySelector('.ai-loading-inline');
    if(old)old.remove();
    const card=document.createElement('div');
    card.className='ai-loading-inline show';
    card.innerHTML='<div class="ai-loading-title">Rakip verileri yükleniyor</div><div class="ai-loading-text" id="aiLoadingText">CHPP geçmiş maç, kadro ve rating bilgileri alınıyor…</div><div class="ai-loading-track"><div class="ai-loading-bar" id="aiLoadingBar"></div></div><div class="ai-loading-percent" id="aiLoadingPercent">7%</div>';
    selected.insertAdjacentElement('afterend',card);
    return card;
  };
  const show=()=>{
    clearTimers();
    currentCard=createCard();
    if(!currentCard)return;
    visible=true;requestId++;setProgress(7,'CHPP geçmiş maç, kadro ve rating bilgileri alınıyor…');
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
    setTimeout(()=>{if(id===requestId)removeCard()},220);
  };

  const originalFetch=window.fetch.bind(window);
  window.fetch=async(...args)=>{
    const input=args[0];
    const url=typeof input==='string'?input:(input?.url||'');
    const tracked=url.includes('/api/fixture-view/');
    if(!tracked)return originalFetch(...args);
    show();
    try{return await originalFetch(...args)}
    finally{hide()}
  };
})();
