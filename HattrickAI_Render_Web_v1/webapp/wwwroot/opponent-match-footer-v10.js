(()=>{
  const V='20260827-opponent-last-match-v10';
  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const view=()=>{try{return typeof currentView!=='undefined'?currentView:null}catch{return null}};
  function latest(){
    const ms=Array.isArray(view()?.recentMatches)?view().recentMatches:[];
    return ms.slice().sort((a,b)=>new Date(b?.fixture?.matchDate||0)-new Date(a?.fixture?.matchDate||0))[0]||null;
  }
  function typeInfo(m){
    const raw=m?.fixture?.matchType??m?.fixture?.matchTypeId??m?.matchType??m?.matchTypeId??'';
    const t=String(raw).toLowerCase(),n=Number(raw);
    if([3,7,9,11].includes(n)||t.includes('cup')||t.includes('kupa'))return {label:'Kupa maçı',icon:'/match-type-icons/cup.svg'};
    if([1,100].includes(n)||t.includes('league')||t.includes('lig'))return {label:'Lig maçı',icon:'/match-type-icons/league.svg'};
    if(n===2||t.includes('qualification')||t.includes('qualifier')||t.includes('eleme'))return {label:'Eleme maçı',icon:null};
    return {label:'Hazırlık maçı',icon:'/match-type-icons/friendly.svg'};
  }
  function markup(m){
    const f=m?.fixture||{},o=m?.opponent||{},ti=typeInfo(m);
    const icon=ti.icon?`<img src="${ti.icon}" alt="${ti.label}" title="${ti.label}">`:'';
    const date=f.matchDate?new Date(f.matchDate).toLocaleDateString('tr-TR',{day:'2-digit',month:'2-digit',year:'numeric'}):'—';
    return `<div class="opponent-last-match-v10"><span class="olm-label">Son maç</span><span class="olm-data">${icon}<span><small>${ti.label} • ${date}</small><b>${esc(f.homeTeamName||'Ev sahibi')} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${esc(f.awayTeamName||'Deplasman')}</b><em>${o.formation||'—'} • ${typeof tacticText==='function'?tacticText(o.tacticType):'Normal'}${o.tacticLevel?` Lv.${o.tacticLevel}`:''}</em></span></span></div>`;
  }
  function hideLegacy(){
    const pitch=document.querySelector('#opponentPitch');if(!pitch)return;
    const panel=pitch.closest('.lineup-panel')||pitch.parentElement;if(!panel)return;
    panel.querySelectorAll('*').forEach(el=>{
      if(el.id==='opponentLastMatchFooter'||el.id==='selectedOpponentMatch')return;
      if(el.children.length===0&&el.textContent.trim()==='Son maç'&&el.getBoundingClientRect().top>=pitch.getBoundingClientRect().bottom-12)el.style.display='none';
      ['::before','::after'].forEach(p=>{try{if(getComputedStyle(el,p).content.includes('Son maç'))el.classList.add('olm-hide-pseudo')}catch{}});
    });
  }
  function render(){
    const m=latest(),pitch=document.querySelector('#opponentPitch');if(!m||!pitch)return;
    const panel=pitch.closest('.lineup-panel')||pitch.parentElement;if(!panel)return;
    hideLegacy();
    let root=panel.querySelector('#opponentLastMatchFooter');
    if(!root){root=document.createElement('div');root.id='opponentLastMatchFooter';pitch.insertAdjacentElement('afterend',root)}
    root.innerHTML=markup(m);
    const selected=document.querySelector('#selectedOpponentMatch');
    if(selected&&typeof applyRecentSelection==='function'){
      try{applyRecentSelection((typeof selectedRecentIndex!=='undefined'?selectedRecentIndex:0))}catch{}
    }
  }
  function style(){
    if(document.getElementById('olm-v10-style'))return;
    const s=document.createElement('style');s.id='olm-v10-style';s.textContent=`
      .olm-hide-pseudo::before,.olm-hide-pseudo::after{content:none!important;display:none!important}
      #opponentLastMatchFooter{display:block;width:100%;box-sizing:border-box;margin:0;padding:0;border:0;overflow:hidden}
      .opponent-last-match-v10{display:flex;align-items:center;justify-content:space-between;gap:8px;min-height:52px;padding:7px 12px;box-sizing:border-box;border-top:1px solid rgba(0,0,0,.08)}
      .olm-label{font-size:12px;color:#777;white-space:nowrap;flex:0 0 auto}
      .olm-data{display:flex;align-items:center;justify-content:flex-end;gap:7px;min-width:0;text-align:right;color:#16834b}
      .olm-data img{width:22px;height:22px;object-fit:contain;flex:0 0 22px}
      .olm-data>span{display:flex;flex-direction:column;min-width:0;line-height:1.15}
      .olm-data small{font-size:8px;color:#777;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      .olm-data b{font-size:10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      .olm-data em{font-style:normal;font-size:7px;color:#777;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      @media(max-width:600px){.opponent-last-match-v10{min-height:50px;padding:6px 10px}.olm-label{font-size:11px}.olm-data img{width:20px;height:20px;flex-basis:20px}.olm-data b{font-size:9px}.olm-data small,.olm-data em{font-size:7px}}
    `;document.head.appendChild(s);
  }
  function boot(){style();render();new MutationObserver(()=>render()).observe(document.body,{childList:true,subtree:true,characterData:true});setInterval(render,1000)}
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(boot,100));else setTimeout(boot,100);
})();
