(function(){
  'use strict';
  const style=document.createElement('style');
  style.textContent='.lineup-head{min-height:92px;padding:13px 18px;gap:14px}.team-logo{width:52px;height:52px;object-fit:contain;flex:0 0 52px}.lineup-info{display:flex;flex-direction:column;justify-content:center}.lineup-title{font-size:20px;line-height:1.08}.team-role{font-size:12px;font-weight:800;color:#2f7d4f;margin-top:5px}.lineup-head .shield{display:none}';
  document.head.appendChild(style);
  function setCard(prefix,name,venue,logo){
    const card=document.getElementById(prefix+'Title')?.closest('.lineup-card');
    if(!card)return;
    card.querySelectorAll('.copy-btn').forEach(x=>x.remove());
    const title=document.getElementById(prefix+'Title');
    const info=title?.closest('.lineup-info');
    const sub=info?.querySelector('.lineup-sub');
    const eyebrow=info?.querySelector('.eyebrow');
    if(eyebrow) eyebrow.textContent='';
    if(sub) sub.remove();
    if(title){title.textContent=name||'';title.style.display=name?'block':'none';}
    const head=title?.closest('.lineup-head');
    if(!head)return;
    let logoEl=head.querySelector('.team-logo');
    if(!logoEl){
      logoEl=document.createElement('img');
      logoEl.className='team-logo';
      logoEl.alt='';
      const old=head.querySelector('.shield');
      if(old) old.replaceWith(logoEl); else head.insertBefore(logoEl,head.firstChild);
    }
    if(logo){logoEl.src=logo;logoEl.style.display='block';}else{logoEl.removeAttribute('src');logoEl.style.display='none';}
    const role=info?.querySelector('.team-role');
    if(role) role.remove();
    if(name && venue){
      const roleEl=document.createElement('div');
      roleEl.className='team-role';
      roleEl.textContent=venue;
      info.appendChild(roleEl);
    }
  }
  function clear(){setCard('own','','','');setCard('opp','','','');}
  function selectedId(){
    const m=document.cookie.match(/(?:^|;\s*)v5\.matchId=([^;]+)/);
    return m?decodeURIComponent(m[1]):'';
  }
  async function load(){
    try{
      const r=await fetch('/api/v5/reference-match?ts='+Date.now(),{cache:'no-store'});
      if(!r.ok)return;
      const d=await r.json();
      const ms=Array.isArray(d.upcomingMatches)?d.upcomingMatches:[];
      if(!ms.length)return;
      const id=selectedId();
      const m=ms.find(x=>String(x.matchId)===String(id))||ms[0];
      const ownName=m.isHome?m.homeTeam:m.awayTeam;
      const ownVenue=m.isHome?'Ev Sahibi':'Deplasman';
      const oppName=m.isHome?m.awayTeam:m.homeTeam;
      setCard('own',ownName,ownVenue,d.ownLogoUrl||'');
      setCard('opp',oppName,m.isHome?'Deplasman':'Ev Sahibi',m.opponentLogoUrl||'');
    }catch(e){}
  }
  function install(){
    clear();
    load();
    const options=document.getElementById('options');
    if(options)options.addEventListener('click',()=>setTimeout(load,50),true);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',install);else install();
})();
