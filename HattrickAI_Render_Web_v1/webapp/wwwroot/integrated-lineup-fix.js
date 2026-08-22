(()=>{
const ROOT='/';
function loadCss(){if(document.getElementById('integratedPlanningWizardCss'))return;const l=document.createElement('link');l.id='integratedPlanningWizardCss';l.rel='stylesheet';l.href=`${ROOT}integrated-planning-wizard.css?v=20260822-wizard-01`;document.head.appendChild(l)}
function loadJs(){return new Promise((resolve,reject)=>{if(window.__integratedPlanningWizardReady){resolve();return}const s=document.getElementById('integratedPlanningWizardJs')||document.createElement('script');s.id='integratedPlanningWizardJs';s.src=`${ROOT}integrated-planning-wizard.js?v=20260822-wizard-01`;s.onload=resolve;s.onerror=()=>reject(new Error('Kadro planı arayüzü yüklenemedi.'));if(!s.parentNode)document.body.appendChild(s)})}
async function openWizard(){loadCss();try{await loadJs();if(typeof window.openIntegratedPlanningWizard==='function')window.openIntegratedPlanningWizard();else throw new Error('Kadro planı arayüzü hazır değil.')}catch(e){console.error(e);alert(e.message||'Kadro planı başlatılamadı.')}}
function install(){const b=document.getElementById('integratedPlanButton');if(!b||b.dataset.wizardBound)return;b.dataset.wizardBound='1';b.removeAttribute('onclick');b.addEventListener('click',e=>{e.preventDefault();e.stopImmediatePropagation();openWizard()},true);b.addEventListener('pointerdown',()=>loadCss(),{passive:true});loadCss()}
new MutationObserver(install).observe(document.documentElement,{childList:true,subtree:true});setInterval(install,700);install();
})();
