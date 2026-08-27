(()=>{
  const VERSION='20260827-responsive-pitch-v6';
  if(window.__responsivePitchV4Installed===VERSION)return;
  window.__responsivePitchV4Installed=VERSION;

  const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const num=(v,d=0)=>{const n=Number(v);return Number.isFinite(n)?n:d};
  const roleText=p=>String(p?.roleKey||p?.role||'').toUpperCase().replace(/\s+/g,'');

  const slots={
    GK:'GK',
    'DEF-L':'DEF-L','DEF-CL':'DEF-CL','DEF-C':'DEF-C','DEF-CR':'DEF-CR','DEF-R':'DEF-R',
    'W-L':'W-L','IM-L':'IM-L','IM-C':'IM-C','IM-R':'IM-R','W-R':'W-R',
    'FW-L':'FW-L','FW-C':'FW-C','FW-R':'FW-R'
  };

  function normalizeSlot(p){
    const r=roleText(p);
    if(r==='GK'||r.includes('GOALKEEP'))return 'GK';
    if(/DEF[-_]L|LB|LEFTBACK/.test(r))return 'DEF-L';
    if(/DEF[-_]CL|LCD/.test(r))return 'DEF-CL';
    if(/DEF[-_]CR|RCD/.test(r))return 'DEF-CR';
    if(/DEF[-_]R|RB|RIGHTBACK/.test(r))return 'DEF-R';
    if(/DEF[-_]C|CENTRALDEF/.test(r))return 'DEF-C';
    if(/W[-_]L|LW|LEFTWING/.test(r))return 'W-L';
    if(/W[-_]R|RW|RIGHTWING/.test(r))return 'W-R';
    if(/IM[-_]L|LIM/.test(r))return 'IM-L';
    if(/IM[-_]R|RIM/.test(r))return 'IM-R';
    if(/IM[-_]C|IM|MIDFIELD/.test(r))return 'IM-C';
    if(/FW[-_]L|LFW|LEFTFORWARD/.test(r))return 'FW-L';
    if(/FW[-_]R|RFW|RIGHTFORWARD/.test(r))return 'FW-R';
    if(/FW[-_]C|FW|FORWARD|STRIKER/.test(r))return 'FW-C';
    return null;
  }

  function fallbackAssign(players){
    const used=new Set();
    const out={};
    const groups={GK:[],DEF:[],MID:[],ATT:[]};
    players.forEach(p=>{
      const r=roleText(p);
      if(normalizeSlot(p)==='GK'||r.includes('KEEP'))groups.GK.push(p);
      else if(/DEF|BACK/.test(r))groups.DEF.push(p);
      else if(/FW|FORWARD|STRIKER/.test(r))groups.ATT.push(p);
      else groups.MID.push(p);
    });
    if(groups.GK[0]){out.GK=groups.GK[0];used.add(groups.GK[0])}
    const defSlots=['DEF-L','DEF-CL','DEF-C','DEF-CR','DEF-R'];
    const midSlots=['W-L','IM-L','IM-C','IM-R','W-R'];
    const attSlots=['FW-L','FW-C','FW-R'];
    defSlots.forEach((s,i)=>{if(groups.DEF[i]){out[s]=groups.DEF[i];used.add(groups.DEF[i])}});
    midSlots.forEach((s,i)=>{if(groups.MID[i]){out[s]=groups.MID[i];used.add(groups.MID[i])}});
    attSlots.forEach((s,i)=>{if(groups.ATT[i]){out[s]=groups.ATT[i];used.add(groups.ATT[i])}});
    return out;
  }

  function assignPlayers(players){
    const assigned={};
    const leftovers=[];
    (players||[]).forEach(p=>{
      const s=normalizeSlot(p);
      if(s&&!assigned[s])assigned[s]=p;
      else leftovers.push(p);
    });
    if(leftovers.length){
      const fallback=fallbackAssign(leftovers);
      Object.entries(fallback).forEach(([s,p])=>{if(!assigned[s])assigned[s]=p});
    }
    return assigned;
  }

  function purgeLegacy(pitch){
    if(!pitch)return;
    /* Old renderers append absolute .player-node elements directly to .pitch.
       The responsive renderer owns the nested .responsive-pitch-slots layer. */
    pitch.querySelectorAll(':scope > .player-node, :scope > .lineup-empty').forEach(n=>n.remove());
    pitch.querySelectorAll(':scope > [data-legacy-pitch-node="1"]').forEach(n=>n.remove());
  }

  function purgeAllPitches(){
    document.querySelectorAll('.pitch').forEach(p=>{
      p.classList.add('responsive-pitch-v4');
      purgeLegacy(p);
    });
  }

  function slotLabel(s){return slots[s]||s}
  function slotClass(s,filled){return `lineup-slot lineup-slot-${s.replace(/[^A-Z0-9]/g,'-')} ${filled?'filled':'empty'}`}

  window.renderPitch=function(target,players,isOpponent=false){
    const pitch=typeof target==='string'?document.querySelector(target):target;
    if(!pitch)return;
    pitch.classList.add('responsive-pitch-v4');

    /* Remove every previous responsive layer, then remove any legacy direct-child layer. */
    pitch.querySelectorAll(':scope > .responsive-pitch-slots').forEach(n=>n.remove());
    purgeLegacy(pitch);

    const layer=document.createElement('div');
    layer.className='responsive-pitch-slots';
    const assigned=assignPlayers(players||[]);
    const order=['GK','DEF-L','DEF-CL','DEF-C','DEF-CR','DEF-R','W-L','IM-L','IM-C','IM-R','W-R','FW-L','FW-C','FW-R'];

    order.forEach(slot=>{
      const cell=document.createElement('div');
      const player=assigned[slot];
      cell.className=slotClass(slot,!!player);
      cell.dataset.slot=slot;
      cell.dataset.position=slotLabel(slot);
      if(player){
        const card=document.createElement('div');
        card.className='player-node responsive-player-node';
        const name=esc(player.name||'');
        const role=esc(player.role||slot);
        const rating=num(player.rating);
        const form=player.form??'-';
        const stamina=player.stamina??'-';
        card.innerHTML=`<span class="shirt">${role}</span><b>${name}</b><strong>${rating.toFixed(2)}</strong><small>FM ${esc(form)} • STA ${esc(stamina)}</small>`;
        card.title=`${name} • ${role} • Rating ${rating.toFixed(2)}`;
        cell.appendChild(card);
      }
      layer.appendChild(cell);
    });
    pitch.appendChild(layer);
  };

  purgeAllPitches();
  const observer=new MutationObserver(records=>{
    document.querySelectorAll('.pitch').forEach(p=>{
      p.classList.add('responsive-pitch-v4');
      purgeLegacy(p);
    });
  });
  observer.observe(document.documentElement,{childList:true,subtree:true});
})();
