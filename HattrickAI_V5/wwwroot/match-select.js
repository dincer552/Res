(function(){
  const MATCH_Q_KEY='__selectedMatch';
  const FOUR_QUESTIONS=[
    {key:MATCH_Q_KEY,title:'Hangi lig maçını analiz etmek istiyorsun?',options:[]},
    {key:'coachStyle',title:'Teknik direktör tarzın nasıl?',options:[['Neutral','Dengeli'],['Offensive','Hücum'],['Defensive','Defans']]},
    {key:'teamSpirit',title:'Takım ruhu hangi seviyede?',options:[['Murderous','Öldürücü'],['Furious','Köpürmüş'],['Irritated','Rahatsız'],['Composed','Kaynaşık'],['Calm','Huzurlu'],['Content','Hoşnut'],['Satisfied','Memnun'],['Delirious','Coşkulu'],['WalkingOnClouds','Bulutların Üzerinde'],['ParadiseOnEarth','Yeryüzünde Cennet']]},
    {key:'matchImportance',title:'Bu maçta hangi yaklaşımı kullanıyorsun?',options:[['Normal','Normal'],['PlayItCool','PIC • Rahat'],['MatchOfTheSeason','MOTS • Çok önemli'],['Auto','OTOMATİK • V5 seçsin']]}
  ];
  let matchOptions=[];
  let q=0;

  function esc(s){return String(s??'').replace(/[&<>\"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));}
  function fmtDate(v){const d=new Date(v);if(Number.isNaN(d.getTime()))return String(v||'');return d.toLocaleString('tr-TR',{day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'});}
  function matchLabel(m){
    return '<span style="display:block;font-family:Arial,Helvetica,sans-serif;font-size:11px;font-weight:700;color:#68716c;margin-bottom:6px">'+esc(fmtDate(m.date))+'</span>'+
      '<span style="display:block;font-family:Georgia,\'Times New Roman\',serif;font-size:15px;font-weight:700;line-height:1.25;color:#27322d">'+esc(m.homeTeam)+' – '+esc(m.awayTeam)+'</span>';
  }
  function setMatchCookie(id){document.cookie='v5.matchId='+encodeURIComponent(String(id))+'; Path=/; Max-Age=28800; SameSite=Lax; Secure';}
  function setupCard(){
    const card=document.getElementById('questionCard');
    const kicker=card?.querySelector('.question-kicker');
    if(kicker) kicker.textContent='SEÇİMLER';
    const title=card?.querySelector('.question-title');
    if(title) title.remove();
    const sub=card?.querySelector('.question-sub');
    if(sub) sub.remove();
    const note=card?.querySelector('.skip-note');
    if(note) note.remove();
    const steps=card?.querySelector('.steps');
    if(steps){steps.innerHTML='';for(let i=0;i<4;i++){const s=document.createElement('i');s.className='step';steps.appendChild(s);}}
  }
  function render(){
    const card=document.getElementById('questionCard');
    const text=document.getElementById('questionText');
    const number=document.getElementById('questionNumber');
    const wrap=document.getElementById('options');
    const next=document.getElementById('next');
    const item=FOUR_QUESTIONS[q];
    selected=answers[item.key]||'';
    number.textContent='SORU '+(q+1)+' / 4';
    text.textContent=item.title;
    document.querySelectorAll('.step').forEach((el,i)=>el.classList.toggle('active',i<=q));
    wrap.innerHTML='';
    const opts=item.key===MATCH_Q_KEY?matchOptions.map(m=>[String(m.matchId),matchLabel(m)]):item.options;
    for(const [value,label] of opts){
      const b=document.createElement('button');
      b.className='option'+(value===selected?' selected':'');
      if(item.key===MATCH_Q_KEY) b.innerHTML=label; else b.textContent=label;
      b.onclick=()=>{
        selected=value;
        answers[item.key]=value;
        if(item.key===MATCH_Q_KEY){
          const m=matchOptions.find(x=>String(x.matchId)===value);
          if(m){setMatchCookie(m.matchId);window.__selectedMatch=m;}
        }
        document.querySelectorAll('.option').forEach(x=>x.classList.remove('selected'));
        b.classList.add('selected');
        next.disabled=false;
      };
      wrap.appendChild(b);
    }
    next.textContent=q===3?'ANALİZİ BAŞLAT':'DEVAM ET';
    next.disabled=!selected;
  }
  async function loadMatches(){
    const error=document.getElementById('error');
    const busy=document.getElementById('busy');
    const button=document.getElementById('analyze');
    error.style.display='none';
    button.disabled=true;
    busy.textContent='CHPP yaklaşan lig maçları okunuyor…';
    busy.style.display='block';
    try{
      const r=await fetch('/api/v5/reference-match?ts='+Date.now(),{cache:'no-store'});
      const data=await r.json().catch(()=>({}));
      if(!r.ok)throw new Error(data.detail||data.message||('CHPP maçları alınamadı (HTTP '+r.status+').'));
      matchOptions=Array.isArray(data.upcomingMatches)?data.upcomingMatches:[];
      if(!matchOptions.length)throw new Error('CHPP üzerinde yaklaşan lig maçı bulunamadı.');
      answers={};q=0;selected='';window.__upcomingMatches=matchOptions;
      setupCard();
      document.getElementById('questionCard').style.display='block';
      render();
      document.getElementById('questionCard').scrollIntoView({behavior:'smooth',block:'nearest'});
    }catch(e){
      error.textContent=e.message;error.style.display='block';
    }finally{busy.style.display='none';button.disabled=false;}
  }
  function install(){
    setupCard();
    const analyze=document.getElementById('analyze');
    const next=document.getElementById('next');
    if(!analyze||!next)return;
    analyze.onclick=loadMatches;
    next.onclick=()=>{
      if(!selected)return;
      if(q<3){q++;render();return;}
      startAnalysis();
    };
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',install);else install();
})();
