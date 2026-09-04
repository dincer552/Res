# V5 Regional Rating / Production Acceptance Roadmap

## FINAL WEB PRODUCTION ACCEPTANCE

A) WEB input integrity       🟢
B) Core ↔ WEB parity         🟡
C) M3 → M11 end-to-end       🟡 **IN PROGRESS — FIRST CHECK: M3 INPUT/OUTPUT CONTINUITY**
D) Gerçek Hattrick input     ⏳
E) Prediction output         ⏳
F) Regression suite          🟡
G) Production smoke test     ⏳

             ↓

      V5 WEB PRODUCTION
             ⏳

---

## C) M3 → M11 END-TO-END

C aşaması gerçek `MotorPipelineService.RunAsync()` zincirinin M3'ten M11'e kadar kesintisiz veri/çıktı akışını doğrular.

### Kabul checklist

- [ ] **M3 input/output continuity** ← **ŞİMDİ BAŞLANAN İLK KONTROL**
- [ ] M4 legal formations
- [ ] M5 XI candidates
- [ ] M6-A candidate evaluation
- [ ] M7 regional rating gerçekten çağrılıyor
- [ ] M7.2 tactical scenario gerçekten çağrılıyor
- [ ] M8 chance model gerçekten çağrılıyor
- [ ] M9 prediction gerçekten çağrılıyor
- [ ] DB1 formation coverage
- [ ] M10 formation competition
- [ ] M10 → M6-B rank-driven handoff
- [ ] M6-B refinement
- [ ] DB2 formation coverage
- [ ] M11 finalist pool
- [ ] M11 final selection
- [ ] FinalPlan continuity
- [ ] FinalPrediction continuity
- [ ] deterministic rerun

### Current C status

**İlk hedef:** M3 input/output continuity.

`MotorPipelineService` içinde M3, `PlayerAnalysisEngine.Analyze(players)` çağrısı ile başlar ve `MotorPipelineResult.M3` olarak downstream'e taşınır. İlk regression kontrolü, pipeline'a giren oyuncu havuzunun M3 çıktısına eksiksiz ve kimlik/sayı sürekliliği ile aktarılmasını doğrulamalıdır.

C'nin diğer maddeleri, ilk M3 kontrolü geçildikten sonra sırayla kilitlenecektir.

> Acceptance semantics: CODED = mekanizma mevcut; REGRESSION = offline test geçiyor; PRODUCTION = gerçek CHPP/match verisiyle doğrulandı.
