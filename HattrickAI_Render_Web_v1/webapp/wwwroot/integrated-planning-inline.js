(()=>{
function injectVisuals(){
  if(document.getElementById('integratedPlanningInlineCss')) return;
  const s=document.createElement('style'); s.id='integratedPlanningInlineCss';
  s.textContent=`
  .ipw-inline-host{width:100%;margin:14px 0 18px;display:block;}
  .ipw-inline-host .ipw-backdrop{position:static!important;inset:auto!important;display:block!important;padding:0!important;background:transparent!important;backdrop-filter:none!important;opacity:1!important;pointer-events:auto!important;}
  .ipw-inline-host .ipw-shell{width:100%!important;max-width:none!important;max-height:none!important;border-radius:22px!important;box-shadow:0 18px 55px rgba(0,0,0,.28)!important;overflow:hidden!important;border:1px solid rgba(99,213,255,.22)!important;}
  .ipw-inline-host .ipw-body{min-height:510px!important;max-height:620px!important;overflow-y:auto!important;overflow-x:hidden!important;scrollbar-width:thin!important;}
  .ipw-inline-host .ipw-top{background:linear-gradient(180deg,#071923,#0a222d);padding:18px 20px 12px!important;position:sticky;top:0;z-index:4;}
  .ipw-inline-host .ipw-brand{align-items:flex-end!important;}
  .ipw-inline-host .ipw-title{font-size:20px!important;letter-spacing:.01em;}
  .ipw-inline-host .ipw-stepname{font-size:10px!important;margin-top:3px;}
  .ipw-inline-host .ipw-progress{height:8px!important;margin-top:14px!important;}
  .ipw-inline-host .ipw-footer{background:#071a23;position:sticky;bottom:0;z-index:4;}
  .ipw-inline-host .ipw-stage{padding:22px!important;min-height:470px;}
  .ipw-inline-host .ipw-stage h3{font-size:23px!important;line-height:1.15;margin-bottom:8px!important;}
  .ipw-inline-host .ipw-stage p{font-size:13px!important;max-width:760px;}
  .ipw-inline-host .ipw-cards{gap:13px!important;}
  .ipw-inline-host .ipw-card{border-radius:20px!important;min-height:105px;box-shadow:0 8px 25px rgba(0,0,0,.14);}
  .ipw-inline-host .ipw-card:hover{transform:translateY(-3px) scale(1.005)!important;}
  .ipw-inline-host .ipw-card.selected{transform:translateY(-2px)!important;box-shadow:0 0 0 2px rgba(53,231,121,.15),0 16px 35px rgba(18,150,89,.16)!important;}
  .ipw-inline-host .ipw-field{height:155px!important;border-radius:16px!important;background:repeating-linear-gradient(90deg,#124b33,#124b33 30px,#14563a 30px,#14563a 60px)!important;}
  .ipw-inline-host .ipw-dot{width:14px!important;height:14px!important;border:3px solid #07161d!important;background:#fff!important;box-shadow:0 0 0 4px rgba(53,231,121,.12),0 5px 12px rgba(0,0,0,.35)!important;}
  .ipw-inline-host .ipw-form-name{font-size:24px!important;letter-spacing:.02em;}
  .ipw-inline-host .ipw-form-exp{font-size:12px!important;}
  .ipw-inline-host .ipw-stage:before{content:'KARAR EKRANI';display:inline-block;font-size:9px;letter-spacing:.16em;color:#63d5ff;margin-bottom:7px;font-weight:900;opacity:.9;}
  .ipw-inline-host .ipw-processing{min-height:430px!important;}
  .ipw-inline-host .ipw-percent{font-size:48px!important;text-shadow:0 0 22px rgba(53,231,121,.24);}
  @media(max-width:700px){.ipw-inline-host .ipw-stage{padding:16px!important}.ipw-inline-host .ipw-body{max-height:70vh!important}.ipw-inline-host .ipw-field{height:130px!important}.ipw-inline-host .ipw-cards.cols2{grid-template-columns:1fr!important}.ipw-inline-host .ipw-form-name{font-size:22px!important}}
  `;
  document.head.appendChild(s);
}
function convertInline(){
  injectVisuals();
  const b=document.getElementById('ipwBackdrop');
  const button=document.getElementById('integratedPlanButton');
  if(!b||!button)return false;
  let host=document.getElementById('integratedPlanningInlineHost');
  if(!host){
    host=document.createElement('div'); host.id='integratedPlanningInlineHost'; host.className='ipw-inline-host';
    button.parentElement?.insertBefore(host,button.nextSibling);
  }
  if(b.parentElement!==host)host.appendChild(b);
  b.classList.add('open');
  return true;
}
window.activateInlinePlanningWizard=convertInline;
window.isInlinePlanningWizardEnabled=()=>!!document.getElementById('integratedPlanningInlineHost');
})();
