(()=>{
const ROOT='/';
const V='20260822-inline-03';
function loadCss(){if(document.getElementById('integratedPlanningWizardCss'))return;const l=document.createElement('link');l.id='integratedPlanningWizardCss';l.rel='stylesheet';l.href=`${ROOT}integrated-planning-wizard.css?v=${V}`;document.head.appendChild(l)}
function loadScript(id,src){return new Promise((resolve,reject)=>{if(document.getElementById(id)){resolve();return}const s=document.createElement('script');s.id=id;s.src=src;s.onload=resolve;s.onerror=()=>reject(new Error(`${id} yüklenemedi.`));document.body.appendChild(s)})}
async function loadJs(){loadCss();if(!window.__integratedPlanningWizardReady)await loadScript('integratedPlanningWizardJs',`${ROOT}integrated-planning-wizard.js?v=${V}`);if(!document.getElementById('integratedPlanningInlineJs'))await loadScript('integratedPlanningInlineJs',`${ROOT}integrated-planning-inline.js?v=${V}`);if(!document.getElementById('integratedPlanningWizardGuardJs'))await loadScript('integratedPlanningWizardGuardJs',`${ROOT}integrated-planning-wizard-guard.js?v=${V}`)}
async function openWizard(){try{await loadJs();if(typeof window.openIntegratedPlanningWizard!=='function')throw new Error('Kadro planı arayüzü hazır değil.');window.openIntegratedPlanningWizard();setTimeout(()=>window.activateInlinePlanningWizard?.(),60)}catch(e){console.error(e);alert(e.message||'Kadro planı başlatılamadı.')}}
function install(){const b=document.getElementById('integratedPlanButton');if(!b||b.dataset.wizardBound)return;b.dataset.wizardBound='1';b.removeAttribute('onclick');b.addEventListener('click',e=>{e.preventDefault();e.stopImmediatePropagation();openWizard()},true);b.addEventListener('pointerdown',()=>loadCss(),{passive:true});loadCss()}
new MutationObserver(install).observe(document.documentElement,{childList:true,subtree:true});setInterval(install,700);install();
})();
