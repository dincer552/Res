(() => {
  const labels = {
    Offensive: ['OFANSİF', 'offensive'],
    Defensive: ['DEFANSİF', 'defensive'],
    TowardsMiddle: ['ORTAYA', 'middle'],
    TowardsWing: ['KANADA', 'wing'],
    Normal: ['NORMAL', 'normal']
  };
  const escape = s => String(s ?? '').replace(/[&<>"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
  const behaviourLabel = value => labels[String(value)] || ['NORMAL', 'normal'];

  window.renderPitch = function(target, players, isOpponent = false) {
    const pitch = document.querySelector(target);
    if (!pitch) return;
    pitch.querySelectorAll('.player-node,.lineup-empty').forEach(n => n.remove());
    if (!players.length) {
      const empty = document.createElement('div');
      empty.className = 'lineup-empty';
      empty.textContent = 'Tarihsel maç kadrosu alınamadı';
      pitch.appendChild(empty);
      return;
    }

    const roleGroup = p => {
      const k = String(p?.roleKey || '').toLowerCase();
      const r = String(p?.role || '').toUpperCase();
      if (k.includes('goalkeeper') || r === 'KL') return 'GK';
      if (k.includes('defender') || ['SLB','SGB','STP'].includes(r)) return 'DEF';
      if (k.includes('forward') || k.includes('striker') || r === 'SF') return 'ATT';
      return 'MID';
    };
    const lineSlots = (count, side, group) => {
      const y = {GK:side ? 10 : 90, DEF:side ? 28 : 72, MID:50, ATT:side ? 72 : 28}[group];
      if (count <= 1) return [[50,y]];
      const presets = {2:[35,65],3:[22,50,78],4:[12,37,63,88],5:[10,30,50,70,90],6:[8,25,42,58,75,92]};
      const xs = presets[Math.min(count,6)] || Array.from({length:count},(_,i)=>8+(84*i/(count-1)));
      return xs.map(x => [x,y]);
    };

    const groups = {GK:[],DEF:[],MID:[],ATT:[]};
    players.forEach(p => groups[roleGroup(p)].push(p));
    Object.entries(groups).forEach(([group,list]) => {
      const slots = lineSlots(list.length, isOpponent, group);
      list.forEach((p,i) => {
        const pos = slots[i];
        const el = document.createElement('div');
        el.className = 'player-node';
        el.style.left = pos[0] + '%';
        el.style.top = pos[1] + '%';
        const [label, css] = behaviourLabel(p.behaviour);
        el.innerHTML = `<span class="shirt">${escape(p.role || '')}</span><b>${escape(p.name)}</b><strong>${Number(p.rating || 0).toFixed(2)}</strong><small>FM ${p.form || '-'} • STA ${p.stamina || '-'}</small><span class="player-behaviour ${css}">${label}</span>`;
        el.title = `${p.name} • ${p.role || ''} • ${label} • Rating ${Number(p.rating || 0).toFixed(2)}`;
        pitch.appendChild(el);
      });
    });
  };
})();