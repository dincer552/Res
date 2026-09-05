(function(){
  function render(data){
    const own=document.getElementById('ownFormation');
    if(!own) return;
    let box=document.getElementById('selectedTacticBox');
    if(!box){
      box=document.createElement('div');
      box.id='selectedTacticBox';
      box.style.cssText='margin:0 18px 15px;padding:13px 15px;border:1px solid #dce4df;border-radius:12px;background:#f7f9f7;font-size:12px;line-height:1.5';
      own.closest('.lineup-card').appendChild(box);
    }
    const t=data?.selectedTactic || data?.tactic || data?.formationDecision || data?.m10 || data?.analysis?.selectedTactic;
    if(!t) return;
    box.innerHTML='<b>🧠 SEÇİLEN TAKTİK</b><br>'+escapeHtml(typeof t==='string'?t:JSON.stringify(t));
  }
  function escapeHtml(s){return String(s).replace(/[&<>\"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'}[m]));}
  const old=window.fetch;
  window.fetch=function(){return old.apply(this,arguments).then(r=>{try{const u=typeof arguments[0]==='string'?arguments[0]:arguments[0].url;if(u.includes('/api/v5/analysis'))r.clone().json().then(render).catch(()=>{});}catch(e){} return r;});};
})();
