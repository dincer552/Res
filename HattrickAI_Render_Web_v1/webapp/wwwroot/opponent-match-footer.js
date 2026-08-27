(()=>{
  const V='20260827-opponent-match-footer-07';
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const typeInfo=m=>{
    const raw=m?.fixture?.matchType ?? m?.fixture?.matchTypeId ?? m?.matchType ?? m?.matchTypeId ?? '';
    const t=String(raw).toLowerCase();
    const n=Number(raw);
    let kind='league',label='Lig maçı',icon='/match-type-icons/league.svg';
    if([2,3,7,9,11].includes(n)||t.includes('cup')||t.includes('kupa')){kind='cup';label='Kupa maçı';icon='/match-type-icons/cup.svg';}
    else if([4,5,8,12,80,101,103,105,106].includes(n)||t.includes('friendly')||t.includes('hazırlık')){kind='friendly';label='Hazırlık maçı';icon='/match-type-icons/friendly.svg';}
    return {kind,label,icon};
  };
  const latestMatch=()=>{
    const ms=Array.isArray(window.currentView?.recentMatches)?window.currentView.recentMatches:[];
    return ms.slice().sort((a,b)=>new Date(b?.fixture?.matchDate||0)-new Date(a?.fixture?.matchDate||0))[0]||null;
  };
  function render(){
    const box=document.querySelector('#selectedOpponentMatch');
    if(!box)return false;
    const m=latestMatch();
    if(!m)return false;
    const f=m.fixture||{};
    const type=typeInfo(m);
    const home=esc(f.homeTeamName||'Ev sahibi');
    const away=esc(f.awayTeamName||'Deplasman');
    const score=`${f.homeGoals??'—'} - ${f.awayGoals??'—'}`;
    const date=f.matchDate?esc(String(f.matchDate).slice(0,10)):'—';
    box.classList.remove('opponent-match-footer');
    box.innerHTML=`<div class="opponent-last-match" style="display:flex;align-items:center;gap:10px;width:100%;box-sizing:border-box;padding:8px 12px;margin:0;background:rgba(20,130,70,.06);border-radius:10px;">
      <img src="${type.icon}" alt="${type.label}" title="${type.label}" style="width:26px;height:26px;flex:0 0 26px;object-fit:contain;">
      <div style="min-width:0;flex:1;">
        <div style="font-size:11px;font-weight:700;line-height:1.25;opacity:.7;">RAKİBİN SON MAÇI • ${type.label}</div>
        <div style="font-size:14px;font-weight:700;line-height:1.35;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${home} <b>${score}</b> ${away}</div>
        <div style="font-size:10px;opacity:.62;margin-top:2px;">${date}</div>
      </div>
    </div>`;
    return true;
  }
  function boot(){
    render();
    const root=document.body;
    if(root&&!root.__opponentLastMatchObserver){
      const observer=new MutationObserver(()=>{if(!document.querySelector('#selectedOpponentMatch .opponent-last-match'))render();});
      observer.observe(root,{childList:true,subtree:true});
      root.__opponentLastMatchObserver=observer;
    }
    let tries=0;
    const timer=setInterval(()=>{tries++;if(render()||tries>=30)clearInterval(timer);},500);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',boot);else boot();
})();
