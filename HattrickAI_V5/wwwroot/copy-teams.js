(function(){
  'use strict';
  var SLOT_MAP={gk:'GK',dl:'DEF-L',dcl:'DEF-CL',dc:'DEF-C',dcr:'DEF-CR',dr:'DEF-R',wl:'W-L',iml:'IM-L',imc:'IM-C',imr:'IM-R',wr:'W-R',fwl:'FW-L',fwc:'FW-C',fwr:'FW-R'};
  function clean(v){return String(v==null?'':v).replace(/\s+/g,' ').trim();}
  function readTeam(prefix){
    var pitch=document.getElementById(prefix+'Pitch');
    var title=clean(document.getElementById(prefix+'Title')&&document.getElementById(prefix+'Title').textContent).replace(/\s*•.*$/,'');
    var formation=clean(document.getElementById(prefix+'Formation')&&document.getElementById(prefix+'Formation').textContent);
    var players=[];
    if(pitch) pitch.querySelectorAll('.slot.filled').forEach(function(slot){
      var name=clean(slot.querySelector('.slot-name')&&slot.querySelector('.slot-name').textContent);
      var raw=clean(slot.querySelector('.slot-rating')&&slot.querySelector('.slot-rating').textContent);
      var rp=raw.replace(/^RP\s*=\s*/i,'').replace(/^RP\s*/i,'');
      var order=clean(slot.querySelector('.slot-order')&&slot.querySelector('.slot-order').textContent) || 'NORMAL';
      var code='';
      Object.keys(SLOT_MAP).some(function(cls){if(slot.classList.contains(cls)){code=SLOT_MAP[cls];return true;}return false;});
      if(name) players.push({position:code,name:name,rp:rp,order:order});
    });
    var ratings={};
    if(pitch){
      var board=pitch.querySelector('.rating-board');
      if(board){
        var vals=[].slice.call(board.querySelectorAll('.rating-row span')).map(function(x){return clean(x.textContent);}).filter(Boolean);
        var mid=board.querySelector('.rating-mid span');
        if(mid) vals.splice(3,0,clean(mid.textContent));
        ['DEF-L','DEF-C','DEF-R','MID','ATT-L','ATT-C','ATT-R'].forEach(function(k,i){if(vals[i]) ratings[k]=vals[i];});
      }
    }
    return {title:title,formation:formation,players:players,ratings:ratings};
  }
  function hasChppData(){
    var own=readTeam('own'),opp=readTeam('opp');
    return own.players.length>=11 && opp.players.length>=11 && Object.keys(own.ratings).length>=7 && Object.keys(opp.ratings).length>=7;
  }
  function textBlock(prefix,opponent){
    var t=readTeam(prefix),labels=['DEF-L','DEF-C','DEF-R','MID','ATT-L','ATT-C','ATT-R'];
    var header=(opponent?'RAKİP ':'')+'HattrickAI V5 KOPYA';
    var playerLines=t.players.map(function(p){return (p.position?p.position+': ':'')+p.name+' | RP='+p.rp+' | '+p.order;}).join('\n');
    return [header,'TAKIM: '+(t.title||'—'),'DİZİLİŞ: '+(t.formation||'—'),'','OYUNCULAR:',playerLines,'','OYUNCU TALİMATLARI / DAVRANIŞLAR:',t.players.map(function(p){return (p.position?p.position+': ':'')+p.order;}).join('\n'),'','BÖLGESEL RATING:'].concat(labels.map(function(l){return l+': '+(t.ratings[l]||'—');})).join('\n');
  }
  async function downloadJson(button){
    var old=button.textContent;
    button.disabled=true;button.textContent='CHPP VERİLERİ ALINIYOR…';
    try{
      var r=await fetch('/api/v5/offline-export?ts='+Date.now(),{cache:'no-store'});
      if(r.status===401) throw new Error('CHPP bağlantısı yok. Önce CHPP bağlantısını tamamla.');
      if(!r.ok){var msg='CHPP offline verisi alınamadı (HTTP '+r.status+').';try{var e=await r.json();if(e&&e.detail)msg=e.detail;}catch(_){ }throw new Error(msg);}
      var data=await r.json();
      var blob=new Blob([JSON.stringify(data,null,2)],{type:'application/json;charset=utf-8'}),url=URL.createObjectURL(blob),a=document.createElement('a');
      a.href=url;a.download='HattrickAI_V5_CHPP_FullOffline_'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';document.body.appendChild(a);a.click();a.remove();
      setTimeout(function(){URL.revokeObjectURL(url);},1000);
      button.textContent='JSON HAZIR ✓';button.classList.add('ok');
      setTimeout(function(){button.textContent=old;button.classList.remove('ok');button.disabled=false;},1800);
    }catch(e){
      button.textContent='HATA: '+(e.message||'JSON oluşturulamadı');
      setTimeout(function(){button.textContent=old;button.disabled=false;},2500);
    }
  }
  function install(){
    var own=document.getElementById('copyOwn'),opp=document.getElementById('copyOpp'),analyze=document.getElementById('analyze');
    if(opp) opp.remove();
    if(own&&own.dataset.combinedCopy!=='1'){
      own.dataset.combinedCopy='1';own.textContent='İKİ TAKIMI KOPYALA';own.title='Kendi takımını ve rakip takımı birlikte kopyala';
      own.onclick=function(event){
        if(event) event.stopPropagation();
        var text=textBlock('own',false)+'\n\n'+textBlock('opp',true),button=own;
        function done(){button.textContent='KOPYALANDI ✓';button.classList.add('ok');setTimeout(function(){button.textContent='İKİ TAKIMI KOPYALA';button.classList.remove('ok');},1400);}
        function fallback(){var a=document.createElement('textarea');a.value=text;a.style.position='fixed';a.style.opacity='0';document.body.appendChild(a);a.focus();a.select();try{document.execCommand('copy');done();}finally{a.remove();}}
        if(navigator.clipboard&&window.isSecureContext) navigator.clipboard.writeText(text).then(done).catch(fallback); else fallback();
      };
    }
    if(analyze&&!document.getElementById('offlineJsonExport')){
      var b=document.createElement('button');b.id='offlineJsonExport';b.type='button';b.className='copy-btn';b.textContent='CHPP VERİLERİNİ JSON AL';b.title='CHPP üzerinden offline motor testleri için gereken tüm verileri yeniden çek ve JSON indir';b.disabled=true;b.style.width='100%';b.style.marginTop='9px';b.style.opacity='.45';
      analyze.parentNode.appendChild(b);
      b.addEventListener('click',function(){if(hasChppData()) downloadJson(b);});
      function refresh(){var ready=hasChppData();b.disabled=!ready;b.style.opacity=ready?'1':'.45';b.title=ready?'CHPP verileri hazır — tüm gerekli CHPP verilerini çekip offline test JSON dosyasını indir':'Önce CHPP verilerinin yüklenmesi bekleniyor';}
      refresh();setInterval(refresh,1000);
    }
  }
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',install); else install();
})();
