(function(){
  'use strict';

  function clean(value){
    return String(value == null ? '' : value).replace(/\s+/g,' ').trim();
  }

  function readTeam(prefix){
    var pitch=document.getElementById(prefix+'Pitch');
    var title=clean(document.getElementById(prefix+'Title') && document.getElementById(prefix+'Title').textContent);
    var formation=clean(document.getElementById(prefix+'Formation') && document.getElementById(prefix+'Formation').textContent);
    var meta=clean(document.getElementById(prefix+'Meta') && document.getElementById(prefix+'Meta').textContent);
    var players=[];

    if(pitch){
      pitch.querySelectorAll('.slot.filled').forEach(function(slot){
        var code=clean(slot.querySelector('.slot-code') && slot.querySelector('.slot-code').textContent);
        var name=clean(slot.querySelector('.slot-name') && slot.querySelector('.slot-name').textContent);
        var rating=clean(slot.querySelector('.slot-rating') && slot.querySelector('.slot-rating').textContent);
        if(name) players.push((code ? code+': ' : '')+name+(rating ? ' | RP='+rating : ''));
      });
    }

    return {title:title,formation:formation,meta:meta,players:players};
  }

  function buildText(){
    var own=readTeam('own');
    var opp=readTeam('opp');
    return [
      'HattrickAI V5 KOPYA',
      'TAKIM: '+(own.title || '—'),
      'DİZİLİŞ: '+(own.formation || '—'),
      '',
      'OYUNCULAR:',
      own.players.join('\n'),
      '',
      'BÖLGESEL RATING:',
      own.meta || '—',
      '',
      'rakip HattrickAI V5 KOPYA',
      'TAKIM: '+(opp.title || '—'),
      'DİZİLİŞ: '+(opp.formation || '—'),
      '',
      'OYUNCULAR:',
      opp.players.join('\n'),
      '',
      'BÖLGESEL RATING:',
      opp.meta || '—'
    ].join('\n');
  }

  function install(){
    var own=document.getElementById('copyOwn');
    var opp=document.getElementById('copyOpp');
    if(opp) opp.remove();
    if(!own || own.dataset.combinedCopy==='1') return;

    own.dataset.combinedCopy='1';
    own.textContent='İKİ TAKIMI KOPYALA';
    own.title='Kendi takımını ve rakip takımı birlikte kopyala';
    own.addEventListener('click',function(){
      var text=buildText();
      var button=own;
      function done(){
        button.textContent='KOPYALANDI ✓';
        button.classList.add('ok');
        window.setTimeout(function(){
          button.textContent='İKİ TAKIMI KOPYALA';
          button.classList.remove('ok');
        },1400);
      }
      if(navigator.clipboard && window.isSecureContext){
        navigator.clipboard.writeText(text).then(done).catch(function(){fallback();});
      }else fallback();
      function fallback(){
        var area=document.createElement('textarea');
        area.value=text;
        area.style.position='fixed';
        area.style.opacity='0';
        document.body.appendChild(area);
        area.focus();
        area.select();
        try{document.execCommand('copy');done();}finally{area.remove();}
      }
    });
  }

  function start(){
    install();
  }

  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',start);
  else start();
})();
