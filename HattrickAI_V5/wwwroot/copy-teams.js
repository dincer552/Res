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
      var rp=raw.replace(/^RP\s*=\s*/i,'').replace(/^SP\s*=\s*/i,'').replace(/^RP\s*/i,'').replace(/^SP\s*/i,'');
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
  function textBlock(prefix,opponent){
    var t=readTeam(prefix),labels=['DEF-L','DEF-C','DEF-R','MID','ATT-L','ATT-C','ATT-R'];
    var header=(opponent?'RAKİP ':'')+'HattrickAI V5 KOPYA';
    var playerLabel=opponent?'SP':'RP';
    var playerLines=t.players.map(function(p){return (p.position?p.position+': ':'')+p.name+' | '+playerLabel+'='+p.rp+' | '+p.order;}).join('\n');
    return [header,'TAKIM: '+(t.title||'—'),'DİZİLİŞ: '+(t.formation||'—'),'','OYUNCULAR:',playerLines,'','OYUNCU TALİMATLARI / DAVRANIŞLAR:',t.players.map(function(p){return (p.position?p.position+': ':'')+p.order;}).join('\n'),'','BÖLGESEL RATING:'].concat(labels.map(function(l){return l+': '+(t.ratings[l]||'—');})).join('\n');
  }
  function install(){
    var own=document.getElementById('copyOwn'),opp=document.getElementById('copyOpp');
    if(opp) opp.remove();
    if(own&&own.dataset.combinedCopy!=='1'){
      own.dataset.combinedCopy='1';own.textContent='İKİ TAKIMI KOPYALA';own.title='Kendi takımını ve rakip takımı birlikte kopyala';
      function combinedCopy(event){
        if(event){event.preventDefault();event.stopImmediatePropagation();}
        var text=textBlock('own',false)+'\n\n================ RAKİP TAKIM ================\n\n'+textBlock('opp',true),button=own;
        function done(){button.textContent='İKİ TAKIM KOPYALANDI ✓';button.classList.add('ok');setTimeout(function(){button.textContent='İKİ TAKIMI KOPYALA';button.classList.remove('ok');},1600);}
        function fallback(){var a=document.createElement('textarea');a.value=text;a.style.position='fixed';a.style.opacity='0';document.body.appendChild(a);a.focus();a.select();try{document.execCommand('copy');done();}finally{a.remove();}}
        if(navigator.clipboard&&window.isSecureContext) navigator.clipboard.writeText(text).then(done).catch(fallback); else fallback();
      }
      own.addEventListener('click',combinedCopy,true);
    }
  }
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',install); else install();
})();