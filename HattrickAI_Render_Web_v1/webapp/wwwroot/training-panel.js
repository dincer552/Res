(function(){
  const role=p=>String(p?.roleKey||'').toLowerCase();
  const group=p=>{const r=role(p);if(r.includes('goalkeeper'))return'GK';if(r.includes('defender'))return'DEF';if(r.includes('midfielder'))return'MID';if(r.includes('winger'))return'WING';if(r.includes('forward'))return'ATT';return''};
  const names={0:'Genel',1:'Dayanıklılık',2:'Duran Toplar',3:'Defans',4:'Golcülük',5:'Kanat Hücumu',6:'Şut',7:'Kısa Paslar',8:'Oyun Kurma',9:'Kalecilik',10:'Ara Paslar',11:'Defansif Pozisyon'};
  function level(type,p){const g=group(p);switch(Number(type)){
    case 0: case 1: case 2: case 6:return 'general';
    case 3:return g==='DEF'?'primary':(g==='GK'||g==='MID'||g==='WING'||g==='ATT'?'secondary':'general');
    case 4:return g==='ATT'?'primary':'secondary';
    case 5:return g==='WING'?'primary':(g==='DEF'||g==='ATT'?'secondary':'general');
    case 7:return ['MID','WING','ATT'].includes(g)?'primary':'secondary';
    case 8:return g==='MID'?'primary':(g==='WING'?'secondary':'general');
    case 9:return g==='GK'?'primary':'general';
    case 10:return ['DEF','MID','WING'].includes(g)?'primary':'general';
    case 11:return ['DEF','MID','WING'].includes(g)?'primary':(g==='GK'?'secondary':'general');
    default:return'general';
  }}
  function label(v){return v==='primary'?'ANA ANTRENMAN':v==='secondary'?'İKİNCİL':'GENEL';}
  function render(x){const panel=document.getElementById('trainingPanel');if(!panel)return;const body=document.getElementById('trainingPanelBody');const toggle=document.getElementById('trainingToggle');const t=x?.training;if(!t){panel.classList.add('hidden');return}panel.classList.remove('hidden');
    const players=(x.ownLineup?.players||[]).map(p=>({...p,priority:level(t.trainingType,p)}));
    const primary=players.filter(p=>p.priority==='primary'),secondary=players.filter(p=>p.priority==='secondary');
    document.getElementById('trainingName').textContent=t.trainingName||names[t.trainingType]||'Antrenman';
    document.getElementById('trainingFormation').textContent=`${x.formation||'—'} • ${x.recommendation?.trainingPriority||'uyum'}`;
    document.getElementById('trainingCount').textContent=`${primary.length} ana / ${secondary.length} ikincil`;
    const sorted=[...primary,...secondary,...players.filter(p=>p.priority==='general')];
    body.innerHTML=`<div class="training-summary"><span class="training-chip important">${escapeHtml(t.trainingName||names[t.trainingType]||'Antrenman')}</span><span class="training-chip">Seviye ${Number(t.trainingLevel??0)}</span><span class="training-chip">Diziliş ${escapeHtml(x.formation||'—')}</span><span class="training-chip">${primary.length} ana oyuncu</span></div><p class="training-note">Bu bölüm, önerilen 11 içinde antrenman açısından korunması gereken oyuncuları gösterir. Maçta oynanan dakika antrenman miktarını etkiler; 90 dakikaya kadar oynanan süre önemlidir.</p><div class="training-list">${sorted.map(p=>`<div class="training-player"><div><div class="training-player-name">${escapeHtml(p.name)}</div><div class="training-player-role">${escapeHtml(p.role||'')}</div></div><span class="training-badge ${p.priority}">${label(p.priority)}</span></div>`).join('')}</div>`;
  }
  function escapeHtml(s){return String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]))}
  function install(){const old=window.renderMatch;if(typeof old!=='function')return;window.renderMatch=function(x){old(x);render(x)};const head=document.getElementById('trainingHead');if(head)head.addEventListener('click',()=>{const p=document.getElementById('trainingPanel');p.classList.toggle('collapsed');document.getElementById('trainingToggle').textContent=p.classList.contains('collapsed')?'+':'−'});}
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',install);else install();
})();