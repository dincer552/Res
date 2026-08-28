(()=>{
const V='20260822-formation-fix-01';
const CSS_ID='formationCompactCss';
const PATCH_ID='formationCompactPatch';
function css(){
 if(document.getElementById(CSS_ID))return;
 const s=document.createElement('style');s.id=CSS_ID;s.textContent=`
 .ipw-cards.cols2:has(.ipw-compact-formation){grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}
 .ipw-card.ipw-compact-formation{min-height:74px!important;height:74px!important;padding:0!important;display:flex!important;align-items:center!important;justify-content:center!important;text-align:center!important;overflow:hidden!important;cursor:pointer!important;transition:transform .16s ease,border-color .16s ease,box-shadow .16s ease!important}
 .ipw-card.ipw-compact-formation .ipw-field,.ipw-card.ipw-compact-formation .ipw-form-exp{display:none!important}
 .ipw-card.ipw-compact-formation .ipw-form-name{font-size:25px!important;font-weight:800!important;letter-spacing:.5px!important;margin:0!important;line-height:1!important}
 .ipw-card.ipw-compact-formation.selected{transform:scale(1.025);box-shadow:0 0 0 2px rgba(45,232,117,.35),0 8px 24px rgba(0,0,0,.18)!important}
 @media(max-width:520px){.ipw-cards.cols2:has(.ipw-compact-formation){grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.ipw-card.ipw-compact-formation{height:68px!important;min-height:68px!important}.ipw-card.ipw-compact-formation .ipw-form-name{font-size:22px!important}}
 `;document.head.appendChild(s)
}
function patch(){
 css();
 const cards=[...document.querySelectorAll('.ipw-card[data-formation]')];
 if(!cards.length)return;
 cards.forEach(card=>{
   if(card.dataset.compactFormation==='1')return;
   card.dataset.compactFormation='1';card.classList.add('ipw-compact-formation');
   card.setAttribute('aria-label',`Formasyon ${card.dataset.formation}`);
   card.addEventListener('click',()=>{
     cards.forEach(x=>x.classList.remove('selected'));
     card.classList.add('selected');
     // Mevcut wizard state handler'ı da çalıştır; ardından kullanıcıyı bekletmeden ilerlet.
     setTimeout(()=>{
       const next=document.getElementById('ipwNext');
       if(next && !next.disabled) next.click();
     },90);
   },false);
 });
}
function observe(){
 if(document.getElementById(PATCH_ID))return;
 const mark=document.createElement('meta');mark.id=PATCH_ID;document.head.appendChild(mark);
 const obs=new MutationObserver(()=>patch());obs.observe(document.documentElement,{childList:true,subtree:true});
 setInterval(patch,250);
 patch();
}
observe();
})();
