(()=>{
  const V='20260827-opponent-match-footer-08';
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const typeInfo=m=>{
    const raw=m?.fixture?.matchType ?? m?.fixture?.matchTypeId ?? m?.matchType ?? m?.matchTypeId ?? '';
    const t=String(raw).toLowerCase();
    const n=Number(raw);
    if([2,3,7,9,11].includes(n)||t.includes('cup')||t.includes('kupa'))return {label:'Kupa maçı',icon:'/match-type-icons/cup.svg'};
    if([4,5,8,12,80,101,103,105,106].includes(n)||t.includes('friendly')||t.includes('hazırlık'))return {label:'Hazırlık maçı',icon:'/match-type-icons/friendly.svg'};
    return {label:'Lig maçı',icon:'/match-type-icons/league.svg'};
  };
  function getView(){try{return typeof currentView!=='undefined'?currentView:null}catch{return null}}
  function getIndex(v){try{return Math.max(0,Math.min(Number(typeof selectedRecentIndex!=='undefined'?selectedRecentIndex:0),((v?.recentMatches?.length||1)-1)))}catch{return 0}}
  function getMatch(){const v=getView();const ms=v?.recentMatches;if(!Array.isArray(ms)||!ms.length)return null;const i=getIndex(v);return ms[i]||null;}
  function makeContent(m){
    const f=m?.fixture||{};const type=typeInfo(m);
    const home=esc(f.homeTeamName||'Ev sahibi'),away=esc(f.awayTeamName||'Deplasman');
    const score=`${f.homeGoals??'—'} - ${f.awayGoals??'—'}`;
    const date=f.matchDate?esc(String(f.matchDate).slice(0,10)):'—';
    return `<div class="opponent-last-match-real" style="display:flex;align-items:center;gap:9px;width:100%;height:100%;box-sizing:border-box;padding:7px 12px;">
      <img src="${type.icon}" alt="${type.label}" title="${type.label}" style="width:25px;height:25px;object-fit:contain;flex:0 0 25px;">
      <div style="min-width:0;line-height:1.2;">
        <div style="font-size:10px;font-weight:800;opacity:.65;">RAKİBİN SON MAÇI • ${type.label}</div>
        <div style="font-size:13px;font-weight:800;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${home} <b>${score}</b> ${away}</div>
        <div style="font-size:9px;opacity:.55;">${date}</div>
      </div>
    </div>`;
  }
  function replaceSonMac(){
    const m=getMatch();if(!m)return false;
    let changed=false;
    document.querySelectorAll('*').forEach(el=>{
      if(el.id==='selectedOpponentMatch')return;
      if(el.children.length===0 && el.textContent.trim()==='Son maç'){
        const parent=el.parentElement;
        if(parent && parent.children.length<=2){
          parent.innerHTML=makeContent(m);
          parent.style.padding='0';parent.style.margin='0';parent.style.minHeight='52px';parent.style.flex='1';parent.style.overflow='hidden';
        }else{
          el.outerHTML=makeContent(m);
        }
        changed=true;
      }
    });
    return changed;
  }
  function boot(){
    replaceSonMac();
    const obs=new MutationObserver(()=>replaceSonMac());
    obs.observe(document.body,{childList:true,subtree:true,characterData:true});
    let n=0;const timer=setInterval(()=>{replaceSonMac();if(++n>=40)clearInterval(timer)},500);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(boot,100));else setTimeout(boot,100);
})();
