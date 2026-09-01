(function(){
  'use strict';
  function add(){
    if(document.getElementById('offlineJsonExport')) return;
    var panel=document.querySelector('section.panel.analysis');
    if(!panel) return;
    var wrap=document.createElement('div');
    wrap.style.cssText='margin-top:10px';
    var btn=document.createElement('button');
    btn.id='offlineJsonExport';
    btn.type='button';
    btn.textContent='OFFLINE TEST VERİSİ · JSON';
    btn.style.cssText='width:100%;height:44px;border:1px solid #cfdad3;border-radius:10px;background:#f7faf8;color:#267448;font:900 12px Arial;cursor:pointer';
    btn.onclick=async function(){
      var old=btn.textContent;
      btn.disabled=true; btn.textContent='JSON HAZIRLANIYOR…';
      try{
        var r=await fetch('/api/v5/analysis?offlineExport='+Date.now(),{cache:'no-store'});
        if(!r.ok) throw new Error('Analiz verisi alınamadı (HTTP '+r.status+').');
        var data=await r.json();
        var payload={
          schema:'hattrickai-v5-offline-test-v1',
          exportedAt:new Date().toISOString(),
          source:'CHPP / V5 analysis pipeline',
          analysis:data
        };
        var blob=new Blob([JSON.stringify(payload,null,2)],{type:'application/json;charset=utf-8'});
        var url=URL.createObjectURL(blob);
        var a=document.createElement('a');
        a.href=url;
        a.download='HattrickAI_V5_OfflineTest_'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(function(){URL.revokeObjectURL(url)},1000);
        btn.textContent='JSON HAZIR ✓';
        setTimeout(function(){btn.textContent=old;btn.disabled=false},1400);
      }catch(e){
        btn.textContent='HATA: '+(e.message||'JSON oluşturulamadı');
        setTimeout(function(){btn.textContent=old;btn.disabled=false},2200);
      }
    };
    wrap.appendChild(btn);
    panel.querySelector('.analysis').appendChild(wrap);
  }
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',add); else add();
  setTimeout(add,500);
})();
