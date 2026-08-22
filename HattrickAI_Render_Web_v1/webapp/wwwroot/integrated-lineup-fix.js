(()=>{
const ROOT='/';
const V='20260822-preload-01';
function loadCss(){if(document.getElementById('integratedPlanningWizardCss'))return;const l=document.createElement('link');l.id='integratedPlanningWizardCss';l.rel='stylesheet';l.href=`${ROOT}integrated-planning-wizard.css?v=${V}`;document.head.appendChild(l)}
function loadScript(id,src){return new Promise((resolve,reject)=>{if(document.getElementById(id)){resolve();return}const s=document.createElement('script');s.id=id;s.src=src;s.onload=resolve;s.onerror=()=>reject(new Error(`${id} yüklenemedi.`));document.body.appendChild(s)})}
async function loadJs(){
  loadCss();
  if(!document.getElementById('integratedPlanningPreloadJs'))await loadScript('integratedPlanningPreloadJs',`${ROOT}integrated-planning-preload.js?v=${V}`);
  if(!window.__integratedPlanningWizardReady)await loadScript('integratedPlanningWizardJs',`${ROOT}integrated-planning-wizard.js?v=${V}`);
  if(!document.getElementById('integratedPlanningInlineJs'))await loadScript('integratedPlanningInlineJs',`${ROOT}integrated-planning-inline.js?v=${V}`);
  if(!document.getElementById('integratedPlanningFormationFixJs'))await loadScript('integratedPlanningFormationFixJs',`${ROOT}integrated-planning-formation-fix.js?v=${V}`);
  if(!document.getElementById('integratedPlanningWizardGuardJs'))await loadScript('integratedPlanningWizardGuardJs',`${ROOT}integrated-planning-wizard-guard.js?v=${V}`);
}
function showLoading(){
  let el=document.getElementById('ipwPreloadStatus');
  if(el)return el;
  el=document.createElement('div');el.id='ipwPreloadStatus';
  el.style.cssText='position:fixed;inset:auto 18px 18px 18px;z-index:99999;background:#071a23;color:#eafff3;border:1px solid rgba(53,231,121,.35);border-radius:16px;padding:14px 16px;box-shadow:0 14px 40px rgba(0,0,0,.35);font:600 14px system-ui;text-align:center';
  el.textContent='Kadro verileri hazırlanıyor…';document.body.appendChild(el);return el;
}
function hideLoading(){document.getElementById('ipwPreloadStatus')?.remove()}
async function openWizard(){
  const loading=showLoading();
  try{
    await loadJs();
    if(typeof window.__ipwPreload==='function'){
      loading.textContent='Takım, fikstür ve rakip geçmişleri alınıyor…';
      await window.__ipwPreload();
      loading.textContent='Tüm veriler hazır. Kadro planı açılıyor…';
    }
    if(typeof window.openIntegratedPlanningWizard!=='function')throw new Error('Kadro planı arayüzü hazır değil.');
    window.openIntegratedPlanningWizard();
    setTimeout(()=>window.activateInlinePlanningWizard?.(),60);
    setTimeout(hideLoading,250);
  }catch(e){console.error(e);loading.textContent=`Veriler alınamadı: ${e.message||'Bilinmeyen hata'}`;setTimeout(hideLoading,2500)}
}
function install(){const b=document.getElementById('integratedPlanButton');if(!b||b.dataset.wizardBound)return;b.dataset.wizardBound='1';b.removeAttribute('onclick');b.addEventListener('click',e=>{e.preventDefault();e.stopImmediatePropagation();openWizard()},true);b.addEventListener('pointerdown',()=>loadCss(),{passive:true});loadCss()}
new MutationObserver(install).observe(document.documentElement,{childList:true,subtree:true});setInterval(install,700);install();
})();
