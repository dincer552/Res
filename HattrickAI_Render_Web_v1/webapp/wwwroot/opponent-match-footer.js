(()=>{
  const V='20260827-opponent-match-footer-10';
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  function getView(){try{return typeof currentView!=='undefined'?currentView:null}catch{return null}}
  function matchType(m){
    const raw=m?.fixture?.matchType??m?.fixture?.matchTypeId??m?.matchType??m?.matchTypeId??'';
    const text=String(raw).toLowerCase();
    const n=Number(raw);
    if([3,7,9,11].includes(n)||text.includes('cup')||text.includes('kupa'))return {label:'Kupa maçı',icon:'/match-type-icons/cup.svg'};
    if([1,100].includes(n)||text.includes('league')||text.includes('lig'))return {label:'Lig maçı',icon:'/match-type-icons/league.svg'};
    if([2].includes(n)||text.includes('qualification')||text.includes('qualifier')||text.includes('eleme'))return {label:'Eleme maçı',icon:null};
    return {label:'Hazırlık maçı',icon:null};
  }
  function latestMatch(){
    const v=getView();
    const ms=Array.isArray(v?.recentMatches)?v.recentMatches:[];
    return ms.slice().sort((a,b)=>new Date(b?.fixture?.matchDate||0)-new Date(a?.fixture?.matchDate||0))[0]||null;
  }
  function matchMarkup(m){
    const f=m?.fixture||{};
    const type=matchType(m);
    const home=esc(f.homeTeamName||'Ev sahibi');
    const away=esc(f.awayTeamName||'Deplasman');
    const score=`${f.homeGoals??'—'} - ${f.awayGoals??'—'}`;
    const date=f.matchDate?esc(String(f.matchDate).slice(0,10)):'—';
    const icon=type.icon?`<img src="${type.icon}" alt="${type.label}" title="${type.label}">`:'';
    return `<span class="opponent-match-value">${icon}<span><small>${type.label} • ${date}</small><b>${home} ${score} ${away}</b></span></span>`;
  }

  // The old UI literally rendered a leaf text node saying "Son maç" below the pitch.
  // Search the whole opponent lineup panel, not just a guessed DOM level, and replace
  // that exact text with the CHPP-derived opponent's most recent match.
  function findOldSonMac(){
    const pitch=document.querySelector('#opponentPitch');
    if(!pitch)return null;
    const panel=pitch.closest('.lineup-panel')||pitch.parentElement;
    if(!panel)return null;
    const p=pitch.getBoundingClientRect();
    const candidates=[...panel.querySelectorAll('*')].filter(el=>{
      if(el.id==='selectedOpponentMatch'||el.id==='opponentLastMatchFooter')return false;
      if(el.children.length!==0)return false;
      return el.textContent.trim()==='Son maç';
    });
    // Prefer the exact "Son maç" element physically below this opponent pitch.
    return candidates
      .filter(el=>el.getBoundingClientRect().top>=p.bottom-8)
      .sort((a,b)=>a.getBoundingClientRect().top-b.getBoundingClientRect().top)[0]||null;
  }

  function render(){
    const m=latestMatch();
    if(!m)return false;
    const pitch=document.querySelector('#opponentPitch');
    if(!pitch)return false;
    const panel=pitch.closest('.lineup-panel')||pitch.parentElement;
    if(!panel)return false;

    const markup=matchMarkup(m);
    const old=findOldSonMac();
    if(old){
      old.outerHTML=markup;
      return true;
    }

    let footer=panel.querySelector('#opponentLastMatchFooter');
    if(!footer){
      footer=document.createElement('div');
      footer.id='opponentLastMatchFooter';
      pitch.insertAdjacentElement('afterend',footer);
    }
    footer.innerHTML=`<span class="opponent-match-label">Rakip dizilişi</span>${markup}`;
    footer.dataset.matchId=String(m?.fixture?.matchId??m?.fixture?.id??'');
    return true;
  }

  function installStyle(){
    if(document.getElementById('opponentLastMatchFooterStyle'))return;
    const style=document.createElement('style');
    style.id='opponentLastMatchFooterStyle';
    style.textContent=`
      #opponentLastMatchFooter{display:flex;align-items:center;justify-content:space-between;gap:8px;padding:8px 14px 9px;min-height:52px;box-sizing:border-box;border-top:1px solid rgba(0,0,0,.08);overflow:hidden}
      #opponentLastMatchFooter .opponent-match-label{font-size:13px;color:#777;white-space:nowrap;flex:0 0 auto}
      #opponentLastMatchFooter .opponent-match-value{display:flex;align-items:center;justify-content:flex-end;gap:7px;min-width:0;text-align:right;color:#16834b}
      #opponentLastMatchFooter .opponent-match-value img{width:23px;height:23px;object-fit:contain;flex:0 0 23px}
      #opponentLastMatchFooter .opponent-match-value>span{display:flex;flex-direction:column;min-width:0;line-height:1.15}
      #opponentLastMatchFooter .opponent-match-value small{font-size:8px;color:#777;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      #opponentLastMatchFooter .opponent-match-value b{font-size:11px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      .opponent-match-value{display:flex;align-items:center;justify-content:flex-end;gap:7px;min-width:0;color:#16834b}
      .opponent-match-value img{width:23px;height:23px;object-fit:contain;flex:0 0 23px}
      .opponent-match-value>span{display:flex;flex-direction:column;min-width:0;line-height:1.15}
      .opponent-match-value small{font-size:8px;color:#777;white-space:nowrap}
      .opponent-match-value b{font-size:11px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      @media(max-width:600px){#opponentLastMatchFooter{padding:8px 12px 9px}#opponentLastMatchFooter .opponent-match-label{font-size:12px}#opponentLastMatchFooter .opponent-match-value b{font-size:10px}#opponentLastMatchFooter .opponent-match-value small{font-size:7px}#opponentLastMatchFooter .opponent-match-value img{width:21px;height:21px;flex-basis:21px}}
    `;
    document.head.appendChild(style);
  }

  function boot(){
    installStyle();
    render();

    // Re-run whenever CHPP/currentView redraws the lineup. The previous observer
    // only reacted when our footer disappeared, so a freshly rendered "Son maç"
    // could remain untouched after CHPP data arrived.
    const observer=new MutationObserver(()=>render());
    observer.observe(document.body,{childList:true,subtree:true,characterData:true});

    // Also cover async CHPP updates that mutate currentView without replacing the DOM.
    let lastSignature='';
    setInterval(()=>{
      const m=latestMatch();
      const sig=m?`${m?.fixture?.matchId??m?.fixture?.id??''}|${m?.fixture?.matchDate??''}|${m?.fixture?.homeGoals??''}|${m?.fixture?.awayGoals??''}`:'';
      const pitch=document.querySelector('#opponentPitch');
      const hasOld=!!findOldSonMac();
      if(sig!==lastSignature||hasOld){lastSignature=sig;render();}
    },500);
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(boot,100));
  else setTimeout(boot,100);
})();
