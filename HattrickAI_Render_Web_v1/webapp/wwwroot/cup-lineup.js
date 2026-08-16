function cupTacticText(t){return({0:'Normal',1:'Pres',2:'Kontra',3:'Ortadan Hücum',4:'Kanatlardan Hücum',7:'Yaratıcı'})[t]||`Taktik ${t??'?'}`}
function cupNum(v,d=0){const n=Number(v);return Number.isFinite(n)?n:d}
function cupEscape(s){return String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]))}
function renderCupLineup(x){
  const r=x.record;
  document.getElementById('cupFormation').textContent=r.formation||'—';
  const f=r.fixture||{},t=r.teamData||{};
  const ratings=t.ratings||{};
  document.getElementById('cupMeta').innerHTML=`<span>${fmtDate(f.matchDate)}</span><b>${cupEscape(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${cupEscape(f.awayTeamName)}</b><span>MF ${cupNum(ratings.midfield).toFixed(2)} • DEF ${((cupNum(ratings.leftDefence)+cupNum(ratings.centralDefence)+cupNum(ratings.rightDefence))/3).toFixed(2)} • ATT ${((cupNum(ratings.leftAttack)+cupNum(ratings.centralAttack)+cupNum(ratings.rightAttack))/3).toFixed(2)}</span><span>${cupTacticText(t.tacticType)} Lv.${t.tacticLevel??'—'}</span>`;
  document.getElementById('cupLineupStatus').textContent=`${r.teamName} • gerçek maç kadrosu • ${x.cache==='PERSISTENT_CACHE'?'kayıtlı veriden':'CHPP’den ilk kez alındı ve kaydedildi'}`;
  const players=(r.players||[]).map(p=>({name:p.name,role:p.role,rating:p.rating,roleKey:p.roleKey,behaviour:p.behaviour,form:'-',stamina:'-'}));
  if(typeof renderPitch==='function') renderPitch('#cupPitch',players,false);
}
async function loadCupLineup(){
  const status=document.getElementById('cupLineupStatus');
  if(!status)return;
  status.textContent='Son kupa kadrosu yükleniyor…';
  try{
    const r=await fetch('/api/cup-lineup/latest');
    const x=await jsonResponse(r);
    if(!r.ok)throw new Error(x.message||'Kupa kadrosu alınamadı.');
    renderCupLineup(x);
  }catch(e){
    status.textContent=e.message||'Kupa kadrosu alınamadı.';
    const meta=document.getElementById('cupMeta');if(meta)meta.innerHTML='';
    const pitch=document.getElementById('cupPitch');if(pitch)pitch.querySelectorAll('.player-node,.lineup-empty').forEach(n=>n.remove());
  }
}
document.addEventListener('DOMContentLoaded',loadCupLineup);
