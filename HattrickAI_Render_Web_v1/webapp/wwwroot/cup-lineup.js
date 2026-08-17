function cupTacticText(t){return({0:'Normal',1:'Pres',2:'Kontra',3:'Ortadan Hücum',4:'Kanatlardan Hücum',7:'Yaratıcı'})[t]||`Taktik ${t??'?'}`}
function cupNum(v,d=0){const n=Number(v);return Number.isFinite(n)?n:d}
function cupEscape(s){return String(s??'').replace(/[&<>\"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]))}

// CHPP matchlineup returns the historical XI with PositionCode/RoleID, not the
// canonical LineupView player shape used by the normal "En iyi 11" renderer.
// Normalize it here before renderPitch; otherwise roleKey is undefined and all
// eleven players fall back to MID and are drawn on one horizontal line.
function cupHistoricalPlayer(p){
  const pc=cupNum(p.positionCode??p.PositionCode,-1);
  const roleId=cupNum(p.roleId??p.RoleId,0);
  let roleKey='CentralMidfielder', role='OM';
  switch(pc){
    case 1: roleKey='Goalkeeper'; role='KL'; break;
    case 2: roleKey='RightDefender'; role='SGB'; break;
    case 3:
    case 4: roleKey='CentralDefender'; role='STP'; break;
    case 5: roleKey='LeftDefender'; role='SLB'; break;
    case 6: roleKey='RightWinger'; role='K'; break;
    case 7:
    case 8: roleKey='CentralMidfielder'; role='OM'; break;
    case 9: roleKey='LeftWinger'; role='K'; break;
    case 10:
    case 11:
      roleKey=roleId===111?'RightForward':roleId===113?'LeftForward':'CentralForward';
      role='SF'; break;
  }
  return {
    name:p.name??p.Name??'Bilinmeyen Oyuncu',
    role,
    roleKey,
    rating:cupNum(p.rating??p.ratingStars??p.RatingStars,0),
    behaviour:p.behaviour??p.Behaviour??'Normal',
    form:p.form??p.Form??'-',
    stamina:p.stamina??p.Stamina??'-'
  };
}

function renderCupLineup(x){
  const r=x.record;
  document.getElementById('cupFormation').textContent=r.formation||'—';
  const f=r.fixture||{},t=r.teamData||{};
  const ratings=t.ratings||{};
  document.getElementById('cupMeta').innerHTML=`<span>${fmtDate(f.matchDate)}</span><b>${cupEscape(f.homeTeamName)} ${f.homeGoals??'—'} - ${f.awayGoals??'—'} ${cupEscape(f.awayTeamName)}</b><span>MF ${cupNum(ratings.midfield).toFixed(2)} • DEF ${((cupNum(ratings.leftDefence)+cupNum(ratings.centralDefence)+cupNum(ratings.rightDefence))/3).toFixed(2)} • ATT ${((cupNum(ratings.leftAttack)+cupNum(ratings.centralAttack)+cupNum(ratings.rightAttack))/3).toFixed(2)}</span><span>${cupTacticText(t.tacticType)} Lv.${t.tacticLevel??'—'}</span>`;
  document.getElementById('cupLineupStatus').textContent=`${r.teamName} • gerçek maç kadrosu • ${x.cache==='PERSISTENT_CACHE'?'kayıtlı veriden':'CHPP’den ilk kez alındı ve kaydedildi'}`;
  const players=(r.players||[]).map(cupHistoricalPlayer);
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
