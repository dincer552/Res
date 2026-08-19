(()=>{
  // Kanat antrenmanı: doğal Kanat adayları öncelikli, ancak genç ve güçlü Defans oyuncuları
  // bek (wing-back) gelişim adayı olarak ayrıca puanlanır.
  const n=(p,k)=>Number(p?.[k]||0);
  const healthy=p=>!p?.injured&&!p?.suspended;
  const role=p=>{const r=p?.position||p?.positionKey||p?.role||p?.roleKey||'';if(['GK','KL','Goalkeeper'].includes(r))return'KL';if(['CD','STP','CentralDefender'].includes(r))return'STP';if(['WB','SLB','SGB','LeftDefender','RightDefender'].includes(r))return'Bek';if(['WING','K','LW','RW','LeftWinger','RightWinger'].includes(r))return'K';if(['IM','OM','CentralMidfielder'].includes(r))return'OM';if(['FWD','SF','LF','CF','RF','LeftForward','CentralForward','RightForward'].includes(r))return'SF';return'OTHER'};
  const ageBonus=a=>a<=17?50:a<=18?47:a<=19?44:a<=20?41:a<=21?40:a<=22?39:a<=23?38:a<=24?37:a<=25?35:a<=26?20:a<=27?10:a<=28?4:a<=29?1:0;
  const score=p=>{
    const a=n(p,'age'),w=n(p,'winger'),d=n(p,'defending'),pa=n(p,'passing'),r=role(p);
    // Kanat ana yetenek, Defans ise bek'e dönüşüm adayı. Kanat > Defans.
    const wing=Math.min(18,w)/18*35;
    const def=Math.min(18,d)/18*27;
    const pass=Math.min(12,pa)/12*12;
    const position=r==='K'?8:r==='Bek'?7:r==='OM'?4:r==='SF'?2:r==='STP'?3:0;
    const youth=a<=17?10:0;
    return ageBonus(a)+wing+def+pass+position+youth;
  };
  const main=p=>{
    const vals=[['Kanat',n(p,'winger'),1],['Defans',n(p,'defending'),.85],['Golcülük',n(p,'scoring'),.70],['Oyun Kurma',n(p,'playmaking'),.55]];
    return vals.reduce((b,x)=>x[1]*x[2]>b.v*b.w?{k:x[0],v:x[1],w:x[2]}:b,{k:'',v:0,w:1});
  };
  async function refreshWingList(){
    if(Number(window.__selectedTrainingType||7)!==2)return;
    const strip=document.getElementById('ownRatingStrip'); if(!strip)return;
    const rows=[...strip.querySelectorAll('[data-training-row]')];
    const data=await fetch('/api/team',{cache:'no-store'}).then(r=>r.json()).catch(()=>null); if(!data?.players)return;
    const byName=new Map(data.players.map(p=>[String(p.name||''),p]));
    const candidates=data.players.filter(healthy).map(p=>({...p,_score:score(p),_main:main(p)})).filter(p=>p.winger>0||p.defending>0).sort((a,b)=>b._score-a._score);
    const title=strip.querySelector('[data-training-list]');
    if(!title)return;
    const html=candidates.map((p,i)=>`<div data-training-row style="display:flex;gap:7px;padding:4px 0;font-size:12px;border-bottom:1px solid rgba(255,255,255,.06)"><b style="width:20px">${i+1}.</b><span style="flex:1"><b>${p.name||'#'+p.playerId}</b> · ${role(p)} · ${n(p,'age')}y</span><span>pas ${n(p,'passing')} · ${p._main.k} ${p._main.v} · Kanat ${n(p,'winger')} · Defans ${n(p,'defending')} · skor ${Math.round(p._score*100)/100}</span></div>`).join('');
    title.innerHTML=html;
  }
  const old=window.__wingTrainingRefresh;
  window.__wingTrainingRefresh=refreshWingList;
  const mo=new MutationObserver(()=>{if(Number(window.__selectedTrainingType||7)===2)refreshWingList()});
  const start=()=>{const el=document.getElementById('ownRatingStrip');if(el)mo.observe(el,{childList:true,subtree:true});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
  window.addEventListener('hattrickai:lineup-updated',()=>setTimeout(refreshWingList,50));
})();