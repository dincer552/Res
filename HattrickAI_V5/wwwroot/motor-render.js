(function () {
  const defs = [
    ['GK','gk'],['DEF-L','dl'],['DEF-CL','dcl'],['DEF-C','dc'],['DEF-CR','dcr'],['DEF-R','dr'],
    ['W-L','wl'],['IM-L','iml'],['IM-C','imc'],['IM-R','imr'],['W-R','wr'],
    ['FW-L','fwl'],['FW-C','fwc'],['FW-R','fwr']
  ];
  const orderLabel = value => {
    const n = typeof value === 'string' ? value : Number(value);
    return ({0:'NORMAL',1:'OFANSİF',2:'DEFANSİF',3:'MERKEZE',4:'KANA'})[n] || '';
  };
  const esc = s => String(s ?? '').replace(/[&<>\"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));
  const fmt = x => Number(x || 0).toFixed(2).replace(/\.00$/,'');
  const ratingValues = r => r ? [r.leftDefence,r.centralDefence,r.rightDefence,r.midfield,r.leftAttack,r.centralAttack,r.rightAttack] : null;

  function board(r) {
    const v = ratingValues(r);
    if (!v) return '';
    return '<div class="rating-board"><div class="rating-row"><span>'+fmt(v[0])+'</span><span>'+fmt(v[1])+'</span><span>'+fmt(v[2])+'</span></div><div class="rating-mid"><span>'+fmt(v[3])+'</span></div><div class="rating-row"><span>'+fmt(v[4])+'</span><span>'+fmt(v[5])+'</span><span>'+fmt(v[6])+'</span></div><div class="rating-label">DEF-L • DEF-C • DEF-R / MID / ATT-L • ATT-C • ATT-R</div></div>';
  }

  window.makePitch = function (target, lineup, rating) {
    const p = document.getElementById(target);
    if (!p) return;
    const isOpponent = target === 'oppPitch';
    p.innerHTML = '<div class="midline"></div><div class="circle"></div><div class="box top"></div><div class="box bot"></div><div class="goal top"></div><div class="goal bot"></div>'+board(rating)+'<div class="slots"></div>';
    const layer = p.querySelector('.slots');
    const players = new Map((lineup?.slots || []).map(s => [s.code, s]));
    for (const [code, cls] of defs) {
      const s = document.createElement('div');
      s.className = 'slot '+cls;
      const x = players.get(code);
      if (x?.playerName) {
        const order = orderLabel(x.order);
        s.classList.add('filled');
        const stars = Number(x.historicalStars);
        const playerValue = isOpponent && Number.isFinite(stars) && stars > 0
          ? 'SP='+stars.toFixed(1)
          : 'RP='+Number(x.rating||0).toFixed(1);
        s.innerHTML = '<span class="slot-name">'+esc(x.playerName)+'</span><span class="slot-rating">'+playerValue+'</span><span class="slot-order '+(order==='OFANSİF'?'off':order==='DEFANSİF'?'def':'')+'">'+order+'</span>';
      } else {
        s.classList.add('empty');
        s.innerHTML = '<span class="slot-name"></span><span class="slot-rating"></span><span class="slot-order"></span>';
      }
      layer.appendChild(s);
    }
  };

  const style = document.createElement('style');
  style.textContent = '.slot-order{font-size:clamp(6px,1.7vw,9px);font-weight:900;line-height:1;margin-top:2px;white-space:nowrap;opacity:.95}.slot-order.off{color:#ffe08a}.slot-order.def{color:#bce9ff}.slot.filled{padding:3px}.slot-name{font-size:clamp(7px,2.15vw,11px)}#ownTitle + .lineup-sub{display:none!important}.lineup-card .copy-btn{display:none!important}#oppReference{display:none!important}#oppMeta{display:none!important}#oppFormation{display:none!important}';
  document.head.appendChild(style);

  const teamHeader = document.createElement('script');
  teamHeader.src = '/team-header.js?v=1';
  document.head.appendChild(teamHeader);
})();
