(()=>{
  const n=(p,k)=>Number(p?.[k]||0);
  const age=a=>a<=17?50:a<=18?47:a<=19?44:a<=20?41:a<=21?40:a<=22?39:a<=23?38:a<=24?37:a<=25?35:a<=26?20:a<=27?10:a<=28?4:a<=29?1:0;
  const pos=p=>String(p?.position||p?.positionKey||p?.role||p?.roleKey||'').toUpperCase();
  const realPos=p=>{const x=pos(p);if(['K','LW','RW','WING','LEFTWINGER','RIGHTWINGER'].includes(x))return'K';if(['STP','CD','CENTRALDEFENDER'].includes(x))return'STP';if(['SLB','SGB','WB','LEFTDEFENDER','RIGHTDEFENDER'].includes(x))return'Bek';if(['OM','IM','CENTRALMIDFIELDER'].includes(x))return'OM';if(['SF','FWD','LF','CF','RF'].includes(x))return'SF';if(['KL','GK','GOALKEEPER'].includes(x))return'KL';return x||'—'};
  const healthy=p=>!p?.injured&&!p?.suspended;
  const wingScore=p=>{const w=n(p,'winger'),a=n(p,'age'),pm=n(p,'playmaking'),pa=n(p,'passing'),s=n(p,'scoring');if(w<=0)return-1e9;return age(a)+(w/18)*42+(Math.min(12,pa)/12)*6+(pm/18)*5+(s/18)*3};
  const wbScore=p=>{const d=n(p,'defending'),w=n(p,'winger'),a=n(p,'age'),pa=n(p,'passing'),pm=n(p,'playmaking');if(d<=0)return-1e9;return age(a)+(d/18)*34+(w/18)*14+(Math.min(12,pa)/12)*5+(pm/18)*3};
  const render=(players)=>{
    const el=document.getElementById('trainingTestPanel')||document.getElementById('ownRatingStrip');if(!el)return;
    const wings=players.filter(healthy).filter(p=>['K','OM','SF'].includes(realPos(p))).map(p=>({...p,_score:wingScore(p)})).filter(p=>p._score>-1e8).sort((a,b)=>b._score-a._score).slice(0,15);
    const backs=players.filter(healthy).filter(p=>['STP','Bek'].includes(realPos(p))).map(p=>({...p,_score:wbScore(p)})).filter(p=>p._score>-1e8).sort((a,b)=>b._score-a._score).slice(0,15);
    const row=(p,i,kind)=>{const main=kind==='wing'?`Kanat ${n(p,'winger')}`:`Defans ${n(p,'defending')}`;const extra=kind==='wing'?`OM ${n(p,'playmaking')} · Gol ${n(p,'scoring')}`:`Kanat ${n(p,'winger')} · OM ${n(p,'playmaking')}`;return `<button type="button" data-wing-select="${p.playerId}" style="display:block;width:100%;text-align:left;border:0;border-bottom:1px solid rgba(255,255,255,.06);background:transparent;color:#d8e4e9;padding:5px 4px;font-size:11px;cursor:pointer"><b>${i+1}.</b> ${p.name||'#'+p.playerId} · ${realPos(p)} · ${n(p,'age')}y · pas ${n(p,'passing')} · ${main} · ${extra} · skor ${p._score.toFixed(2)}</button>`};
    el.innerHTML=`<div style="margin-top:10px;display:grid;grid-template-columns:1fr 1fr;gap:10px"><div><div style="font-weight:800;font-size:12px;margin-bottom:5px">Gerçek Kanat · %100</div>${wings.map((p,i)=>row(p,i,'wing')).join('')}</div><div><div style="font-weight:800;font-size:12px;margin-bottom:5px">Bek Adayı · %50</div>${backs.map((p,i)=>row(p,i,'back')).join('')}</div></div>`;
    el.querySelectorAll('[data-wing-select]').forEach(b=>b.onclick=()=>{const id=b.dataset.wingSelect;window.__selectedTrainingPlayerId=id;el.querySelectorAll('[data-wing-select]').forEach(x=>x.style.background='transparent');b.style.background='#163747';window.dispatchEvent(new CustomEvent('hattrickai:training-player-selected',{detail:{playerId:id,trainingType:2}}));});
  };
  const hook=()=>{if(Number(window.__selectedTrainingType||0)!==2)return;fetch('/api/team',{cache:'no-store'}).then(r=>r.json()).then(t=>render(t.players||[])).catch(()=>{});};
  const obs=new MutationObserver(()=>hook());
  const start=()=>{const e=document.getElementById('ownRatingStrip');if(e)obs.observe(e,{childList:true,subtree:true});hook()};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(start,600));else setTimeout(start,600);
  window.addEventListener('hattrickai:lineup-updated',hook);
})();