(()=>{
  const ICONS={league:'/match-type-icons/league.svg',cup:'/match-type-icons/cup.svg',friendly:'/match-type-icons/friendly.svg'};
  const LABELS={league:'Lig maçı',cup:'Kupa maçı',friendly:'Hazırlık maçı'};
  const TYPE={
    league:new Set([1,100]),
    cup:new Set([2,3,7,9,11]),
    friendly:new Set([4,5,8,12,80,101,103,105,106])
  };
  const kind=t=>{t=Number(t);if(TYPE.cup.has(t))return'cup';if(TYPE.friendly.has(t))return'friendly';return'league'};
  const idFromCard=card=>{const m=(card.getAttribute('onclick')||'').match(/\((\d+)\)/);return m?Number(m[1]):0};
  const addIcon=(card,type)=>{
    const k=kind(type);card.dataset.matchType=String(type);card.dataset.matchKind=k;
    let img=card.querySelector('.match-type-icon');
    if(!img){img=document.createElement('img');img.className='match-type-icon';card.prepend(img)}
    img.src=ICONS[k];img.alt=LABELS[k];img.title=LABELS[k];
  };
  const decorateFixtures=async()=>{
    const box=document.querySelector('#fixturesList');if(!box)return;
    try{
      const r=await fetch('/api/fixtures');if(!r.ok)return;
      const data=await r.json();const byId=new Map((data.fixtures||[]).map(f=>[Number(f.matchId),f]));
      box.querySelectorAll('.fixture-card').forEach(card=>{const f=byId.get(idFromCard(card));if(f)addIcon(card,f.matchType)});
    }catch{}
  };
  const decorateRecent=(matches)=>{
    document.querySelectorAll('#recentMatches .recent-card').forEach((card,i)=>{
      const m=matches?.[i];if(!m?.fixture)return;
      const k=kind(m.fixture.matchType);card.dataset.matchType=String(m.fixture.matchType);card.dataset.matchKind=k;
      let img=card.querySelector('.match-type-icon');
      if(!img){img=document.createElement('img');img.className='match-type-icon';card.prepend(img)}
      img.src=ICONS[k];img.alt=LABELS[k];img.title=LABELS[k];
    });
  };
  const style=document.createElement('style');
  style.textContent=`
    .fixture-card,.recent-card{position:relative}
    .fixture-card{grid-template-columns:30px 1fr 24px 1fr;padding-left:9px}
    .fixture-card .fixture-date,.fixture-card small{grid-column:1/-1}
    .fixture-card .match-type-icon{grid-column:1;grid-row:2/4;width:25px;height:25px;object-fit:contain;align-self:center;justify-self:center}
    .fixture-card b:nth-of-type(1){grid-column:2}.fixture-card em{grid-column:3}.fixture-card b:nth-of-type(2){grid-column:4}
    .recent-card{padding-left:40px}.recent-card .match-type-icon{position:absolute;left:9px;top:10px;width:24px;height:24px;object-fit:contain}
    @media(max-width:600px){.fixture-card{grid-template-columns:27px 1fr 22px 1fr}.fixture-card .match-type-icon{width:23px;height:23px}.recent-card .match-type-icon{width:22px;height:22px}}
  `;
  document.head.appendChild(style);
  const observer=new MutationObserver(()=>decorateFixtures());
  const start=()=>{const box=document.querySelector('#fixturesList');if(box)observer.observe(box,{childList:true});decorateFixtures()};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  const oldLoad=window.loadFixtures;
  if(typeof oldLoad==='function')window.loadFixtures=async()=>{await oldLoad();await decorateFixtures()};
  const oldRecent=window.renderRecent;
  if(typeof oldRecent==='function')window.renderRecent=(matches)=>{oldRecent(matches);decorateRecent(matches)};
})();