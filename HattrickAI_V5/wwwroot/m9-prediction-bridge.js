(function(){
  'use strict';
  var boxId='v5M9PredictionBox';
  function ensure(){
    var box=document.getElementById(boxId);
    if(box)return box;
    box=document.createElement('section');
    box.id=boxId;
    box.style.cssText='margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1';
    box.innerHTML='<div style="padding:13px 16px;border-bottom:1px solid #e5ebe7;font:900 14px Arial;color:#27322d">🎯 M9 Maç Tahmini</div><div id="v5M9PredictionBody" style="padding:14px"></div>';
    var host=document.querySelector('.analysis')||document.querySelector('main.page')||document.body;
    var motor=document.getElementById('v5MotorLogBox');
    if(motor&&motor.parentNode)motor.parentNode.insertBefore(box,motor);else host.appendChild(box);
    return box;
  }
  function pct(v){return Number.isFinite(Number(v))?(Number(v)*100).toFixed(1)+'%':'—'}
  function num(v){return Number.isFinite(Number(v))?Number(v).toFixed(2):'—'}
  function esc(v){return String(v??'').replace(/[&<>\"']/g,function(m){return({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#039;'})[m]})}
  function render(x){
    if(!x)return;
    var m=x.m9Prediction||x.m9||(x.motorPipeline&&x.motorPipeline.m9);
    var p=(m&&m.prediction)||x.finalPrediction||x.prediction;
    if(!p)return;
    var body=ensure().querySelector('#v5M9PredictionBody');
    var win=Number(p.winProbability),draw=Number(p.drawProbability),loss=Number(p.lossProbability);
    var result=(m&&m.predictedResult)||(win>=draw&&win>=loss?'Galibiyet':draw>=loss?'Beraberlik':'Rakip Galibiyeti');
    var score=(m&&m.mostLikelyScore)||'—';
    var sim=p.simulation||(m&&m.simulation)||{};
    body.innerHTML='<div style="display:flex;justify-content:space-between;gap:12px;align-items:center;flex-wrap:wrap">'+
      '<div><div style="font:900 20px Arial">'+esc(result)+'</div><div style="margin-top:4px;color:#59625d;font:800 12px Arial">'+esc(x.teamName||'Bizim takım')+' vs '+esc(x.opponentName||'Rakip')+'</div></div>'+ 
      '<div style="text-align:right"><div style="font:900 18px Arial">'+esc(score)+'</div><div style="color:#7a827d;font:11px Arial">En olası analitik skor</div></div></div>'+ 
      '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:14px">'+
      '<div style="background:#f7f9f7;border-radius:10px;padding:10px;text-align:center"><div style="color:#7a827d;font:800 10px Arial">Galibiyet</div><div style="font:900 16px Arial">'+pct(win)+'</div></div>'+ 
      '<div style="background:#f7f9f7;border-radius:10px;padding:10px;text-align:center"><div style="color:#7a827d;font:800 10px Arial">Beraberlik</div><div style="font:900 16px Arial">'+pct(draw)+'</div></div>'+ 
      '<div style="background:#f7f9f7;border-radius:10px;padding:10px;text-align:center"><div style="color:#7a827d;font:800 10px Arial">Rakip kazanır</div><div style="font:900 16px Arial">'+pct(loss)+'</div></div></div>'+ 
      '<div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:10px">'+
      '<div style="background:#fafbfa;border:1px solid #edf0ee;border-radius:9px;padding:9px 10px"><div style="color:#7a827d;font:800 10px Arial">Beklenen gol</div><div style="margin-top:3px;font:900 13px Arial">'+num(p.expectedHomeGoals)+' - '+num(p.expectedAwayGoals)+'</div></div>'+ 
      '<div style="background:#fafbfa;border:1px solid #edf0ee;border-radius:9px;padding:9px 10px"><div style="color:#7a827d;font:800 10px Arial">Topa sahip olma</div><div style="margin-top:3px;font:900 13px Arial">'+pct(p.possessionProbability)+'</div></div></div>'+ 
      '<div style="margin-top:12px;padding:11px;background:#fafbfa;border-radius:10px;border:1px solid #edf0ee"><div style="font:900 12px Arial">🎲 1000x Simülasyon</div><div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:9px">'+
      '<div><div style="color:#7a827d;font:800 10px Arial">Sim. galibiyet</div><div style="font:900 13px Arial">'+pct(sim.outcome&&sim.outcome.winProbability)+'</div></div>'+ 
      '<div><div style="color:#7a827d;font:800 10px Arial">Sim. beraberlik</div><div style="font:900 13px Arial">'+pct(sim.outcome&&sim.outcome.drawProbability)+'</div></div>'+ 
      '<div><div style="color:#7a827d;font:800 10px Arial">Sim. rakip</div><div style="font:900 13px Arial">'+pct(sim.outcome&&sim.outcome.lossProbability)+'</div></div></div>'+ 
      '<div style="margin-top:8px;color:#7a827d;font:10px Arial">En sık skor: '+esc(sim.mostLikelyScore||'—')+' • En sık sonuç: '+esc(sim.mostLikelyResult||'—')+'</div></div>';
  }
  function wire(){
    window.addEventListener('v5:analysis-ready',function(e){render(e.detail)});
    var originalFetch=window.fetch;
    window.fetch=async function(){
      var response=await originalFetch.apply(this,arguments);
      try{
        var input=arguments[0],url=typeof input==='string'?input:input&&input.url||'';
        if(String(url).includes('/api/v5/analysis')&&response.ok){
          response.clone().json().then(function(data){render(data);window.dispatchEvent(new CustomEvent('v5:analysis-ready',{detail:data}))}).catch(function(){});
        }
      }catch(_){}
      return response;
    };
  }
  wire();
})();
