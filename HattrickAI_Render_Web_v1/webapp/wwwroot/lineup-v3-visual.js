/* V3 lineup visual layer. Keeps the 14 Hattrick starting-position frames visible before CHPP data arrives. */
(()=>{
  const slots=[
    {id:'GK',code:'GK',desc:'Kaleci',x:50,y:10,group:'GK'},
    {id:'DL',code:'DEF-L',desc:'Sol bek',x:10,y:28,group:'DEF'},
    {id:'DCL',code:'DEF-CL',desc:'Sol stoper',x:30,y:28,group:'DEF'},
    {id:'DC',code:'DEF-C',desc:'Merkez stoper',x:50,y:28,group:'DEF'},
    {id:'DCR',code:'DEF-CR',desc:'Sağ stoper',x:70,y:28,group:'DEF'},
    {id:'DR',code:'DEF-R',desc:'Sağ bek',x:90,y:28,group:'DEF'},
    {id:'WL',code:'W-L',desc:'Sol kanat',x:10,y:50,group:'MID'},
    {id:'IML',code:'IM-L',desc:'Sol iç',x:30,y:50,group:'MID'},
    {id:'IMC',code:'IM-C',desc:'Merkez',x:50,y:50,group:'MID'},
    {id:'IMR',code:'IM-R',desc:'Sağ iç',x:70,y:50,group:'MID'},
    {id:'WR',code:'W-R',desc:'Sağ kanat',x:90,y:50,group:'MID'},
    {id:'FWL',code:'FW-L',desc:'Sol forvet',x:25,y:76,group:'ATT'},
    {id:'FWC',code:'FW-C',desc:'Merkez forvet',x:50,y:76,group:'ATT'},
    {id:'FWR',code:'FW-R',desc:'Sağ forvet',x:75,y:76,group:'ATT'}
  ];

  const formationSlots={
    '4-3-3':['GK','DL','DCL','DCR','DR','IML','IMC','IMR','FWL','FWC','FWR'],
    '3-5-2':['GK','DCL','DC','DCR','WL','IML','IMC','IMR','WR','FWL','FWC'],
    '4-5-1':['GK','DL','DCL','DCR','DR','WL','IML','IMC','IMR','WR','FWC'],
    '5-4-1':['GK','DL','DCL','DC','DCR','DR','WL','IMC','IMR','WR','FWC'],
    '5-3-2':['GK','DL','DCL','DC','DCR','DR','IML','IMC','IMR','FWL','FWC'],
    '3-4-3':['GK','DCL','DC','DCR','WL','IML','IMR','WR','FWL','FWC','FWR']
  };

  const esc=v=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const n=(v,d=0)=>{const x=Number(v);return Number.isFinite(x)?x:d};
  const byId=Object.fromEntries(slots.map(s=>[s.id,s]));

  function playerSlot(player, index, formation){
    const role=String(player?.roleKey||'').toLowerCase();
    const code=String(player?.role||'').toUpperCase();
    const pc=n(player?.positionCode,0);
    const mapByCode={
      1:'GK',2:'DR',3:'DCR',4:'DC',5:'DL',6:'WR',7:'IMR',8:'IMC',9:'WL',10:'FWR',11:'FWC'
    };
    if(mapByCode[pc]) return mapByCode[pc];
    const occupied=formationSlots[formation]||formationSlots['4-4-2']||[];
    const exact={
      goalkeeper:'GK',rightdefender:'DR',leftdefender:'DL',
      rightwinger:'WR',leftwinger:'WL',
      rightinnermidfielder:'IMR',leftinnermidfielder:'IML',centralmidfielder:'IMC',
      rightforward:'FWR',leftforward:'FWL',centralforward:'FWC'
    };
    if(exact[role]) return exact[role];
    if(role.includes('goalkeeper')||code==='KL') return 'GK';
    const group=role.includes('defender')||['SLB','SGB','STP'].includes(code)?'DEF':role.includes('forward')||code==='SF'?'ATT':'MID';
    const candidates=slots.filter(s=>s.group===group && !window.__v3UsedSlots?.has(s.id));
    return candidates[0]?.id || occupied[index] || null;
  }

  function buildPitch(pitch,players,formation){
    if(!pitch) return;
    pitch.classList.add('v3-pitch');
    pitch.querySelectorAll('.player-node,.lineup-empty,.v3-slot').forEach(x=>x.remove());
    const used=new Set();
    window.__v3UsedSlots=used;
    const assigned={};
    (players||[]).forEach((p,i)=>{
      let id=playerSlot(p,i,formation);
      if(id && used.has(id)){
        const s=slots.find(x=>x.id===id);
        const alternatives=slots.filter(x=>x.group===s?.group&&!used.has(x.id));
        id=alternatives[0]?.id||null;
      }
      if(id){used.add(id);assigned[id]=p;}
    });
    slots.forEach(s=>{
      const p=assigned[s.id];
      const el=document.createElement('div');
      el.className='v3-slot '+(p?'is-filled':'is-empty');
      el.style.left=s.x+'%';el.style.top=s.y+'%';
      if(p){
        const role=p.role||s.code;
        el.innerHTML=`<span class="slot-code">${esc(role)}</span><span class="slot-name">${esc(p.name||'Oyuncu')}</span><span class="slot-desc">${esc(s.desc)}</span><span class="slot-rating">Rating katkısı: ${n(p.rating).toFixed(2)}</span>`;
        el.title=`${p.name||'Oyuncu'} • ${role} • ${n(p.rating).toFixed(2)}`;
      }else{
        el.innerHTML=`<span class="slot-code">${esc(s.code)}</span><span class="slot-name">Boş pozisyon</span><span class="slot-desc">${esc(s.desc)}</span>`;
        el.title=`Boş pozisyon: ${s.code}`;
      }
      pitch.appendChild(el);
    });
  }

  function ensureControls(grid){
    if(document.querySelector('.v3-control-panel')) return;
    const panel=document.createElement('section');
    panel.className='panel v3-control-panel';
    panel.innerHTML=`<div class="v3-control-row"><strong>Taktik</strong><span class="v3-normal">Normal <span class="v3-chevron">⌄</span></span></div><div class="v3-control-row"><strong>Takım Davranışı</strong><span class="v3-normal">Normal <span class="v3-chevron">⌄</span></span></div><div class="v3-timeline"><span class="arrow">‹</span><div class="track"><i class="dot"></i></div><span class="minute">0'</span><span class="arrow">›</span></div>`;
    grid.parentNode.insertBefore(panel,grid.nextSibling);
  }

  function addFooter(card,formation,label){
    if(!card) return;
    let footer=card.querySelector('.v3-lineup-footer');
    if(!footer){footer=document.createElement('div');footer.className='v3-lineup-footer';card.appendChild(footer)}
    footer.innerHTML=`<span>${esc(label)}</span><strong>${esc(formation||'—')}</strong>`;
  }

  function initial(){
    const grid=document.querySelector('.lineup-grid');
    if(!grid) return;
    grid.classList.add('v3-lineup-grid');
    const cards=[...grid.querySelectorAll('.lineup-panel')];
    cards.forEach((card,i)=>{
      card.classList.add('v3-lineup-card');
      const pitch=card.querySelector('.pitch');
      if(pitch) buildPitch(pitch,[],i===0?'—':'—');
    });
    const own=grid.querySelector('.our-panel');
    const opp=[...grid.querySelectorAll('.lineup-panel')].find(x=>x!==own);
    addFooter(own,'—','Önerilen diziliş');
    addFooter(opp,'—','Rakip diziliş');
    ensureControls(grid);
  }

  window.v3RenderPitch=(target,players,isOpponent=false)=>{
    const pitch=typeof target==='string'?document.querySelector(target):target;
    const formation=(isOpponent?window.currentView?.opponentLineup?.formation:window.currentView?.ownLineup?.formation)||'—';
    buildPitch(pitch,players||[],formation);
    const card=pitch?.closest('.lineup-panel');
    addFooter(card,formation,isOpponent?'Rakip diziliş':'Önerilen diziliş');
  };

  // Override the global renderer after app.js has loaded. renderMatch continues to call renderPitch,
  // but the global function is replaced here so the existing CHPP/data flow remains untouched.
  const old=window.renderPitch;
  window.renderPitch=(target,players,isOpponent=false)=>{
    try{window.v3RenderPitch(target,players,isOpponent)}catch(e){console.error('V3 lineup render',e);if(typeof old==='function')old(target,players,isOpponent)}
  };

  document.addEventListener('DOMContentLoaded',initial,{once:true});
  if(document.readyState!=='loading') initial();
})();
