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
      var code='';
      Object.keys(SLOT_MAP).some(function(cls){if(slot.classList.contains(cls)){code=SLOT_MAP[cls];return true;}return false;});
      if(name) players.push((code?code+': ':'')+name+' | RP='+rp);
    });
    var values=[];
    if(pitch){
      var board=pitch.querySelector('.rating-board');
      if(board) values=[].slice.call(board.querySelectorAll('.rating-row span')).map(function(x){return clean(x.textContent);}).filter(Boolean);
      var mid=board&&board.querySelector('.rating-mid span');
      if(mid) values.splice(3,0,clean(mid.textContent));
    }
    return {title:title,formation:formation,players:players,ratings:values};
  }
  function block(prefix,opponent){
    var t=readTeam(prefix),labels=['DEF-L','DEF-C','DEF-R','MID','ATT-L','ATT-C','ATT-R'];
    return [(opponent?'RAKİP ':'')+'HattrickAI V5 KOPYA','TAKIM: '+(t.title||'—'),'DİZİLİŞ: '+(t.formation||'—'),'','OYUNCULAR:',t.players.join('\n'),'','BÖLGESEL RATING:'].concat(labels.map(function(l,i){return l+': '+(t.ratings[i]||'—');})).join('\n');
  }
  function install(){
    var own=document.getElementById('copyOwn'),opp=document.getElementById('copyOpp');
    if(opp) opp.remove();
    if(!own||own.dataset.combinedCopy==='1') return;
    own.dataset.combinedCopy='1';own.textContent='İKİ TAKIMI KOPYALA';own.title='Kendi takımını ve rakip takımı birlikte kopyala';
    own.addEventListener('click',function(){
      var text=block('own',false)+'\n\n'+block('opp',true),button=own;
      function done(){button.textContent='KOPYALANDI ✓';button.classList.add('ok');setTimeout(function(){button.textContent='İKİ TAKIMI KOPYALA';button.classList.remove('ok');},1400);}
      function fallback(){var a=document.createElement('textarea');a.value=text;a.style.position='fixed';a.style.opacity='0';document.body.appendChild(a);a.focus();a.select();try{document.execCommand('copy');done();}finally{a.remove();}}
      if(navigator.clipboard&&window.isSecureContext) navigator.clipboard.writeText(text).then(done).catch(fallback); else fallback();
    });
  }
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',install); else install();
})();
