# HattrickAI V5

## FINAL WEB PRODUCTION ACCEPTANCE

Aktif branch: `v5`. V5 hedefi: oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB zincirini tek ve tutarlı maç motoru olarak çalıştırmak.

### C) M3 → M11 end-to-end acceptance

İlerleme sırası:

```text
C1  M3 input/output continuity       🟡 CONTRACT EXISTS
C2  M4 legal formations              ✅ REGRESSION
C3  M5 XI candidates                 ✅ REGRESSION
C4  M6-A candidate evaluation        🟡 CONTRACT IMPLEMENTED — CI PENDING
C5  M7 regional rating               ⏳
C6  M7.2 tactical scenario           ⏳
C7  M8 chance model                  ⏳
C8  M9 prediction                    ⏳
C9  DB1 formation coverage           ⏳
C10 M10 formation competition        ⏳
C11 M10 → M6-B rank-driven handoff   ⏳
C12 M6-B refinement                  ⏳
C13 DB2 formation coverage            ⏳
C14 M11 finalist pool                 ⏳
C15 M11 final selection               ⏳
C16 FinalPlan continuity              ⏳
C17 FinalPrediction continuity        ⏳
C18 deterministic rerun               ⏳
```

#### C4 — M6-A Candidate Evaluation

`HattrickAI_V5.OfflineTests/M6ACandidateEvaluationRegression.cs` eklendi ve offline runner'a bağlandı. Regression gerçek `MotorPipelineService` üzerinden M5 XI havuzunu M6-A global search'e verir.

Kabul sözleşmesi:

```text
M6-A evaluatedCandidates > 0
her legal formasyon için en az bir değerlendirme
M6-A BestCandidate mevcut
M6-A ranked TopCandidates mevcut
Candidate DB #1 boş değil
DB1 en az bir aday / legal formasyon
retained candidate = 11 slot / 11 unique player / 11 unique slot
TacticalScore finite ve >= 0
retained pool tüm legal formasyonları kapsıyor
retained candidate signature'ları unique
BestCandidate retained pool içinde
M6-A iteration state anlamlı
```

M6-A kodu M5 adaylarını formasyon bazında search eder; her başlangıç XI için candidate evaluator çağrılır, tactical candidate sonuçları M6 database'ine alınır ve en iyi aday deterministik tie-break ile seçilir. Pipeline callback'i aynı değerlendirme sırasında M7 → M7.2 → M8 → M9 downstream zincirini çalıştırıp DB1 kaydını oluşturur.

**CI notu:** C4 kontratı commit edilmiştir; mevcut CI altyapısında bu yeni commit için doğrulanmış PASS sonucu henüz alınmadığından C4 `CI PENDING` durumundadır.

### A) WEB input integrity

`WebInputIntegrityRegression` offline suite'in ilk kontrolüdür. WEB questionnaire alanlarını, saha slotlarını, endpoint/session akışını ve AnalysisService CHPP veri akışını statik sözleşme seviyesinde kontrol eder.

### B) Core ↔ WEB parity

`CoreWebParityRegression` offline suite'te A testinden sonra çalışır ve Core Analysis → camelCase JSON → `/api/v5/analysis` → WEB binding sözleşmesini kontrol eder. Canlı CHPP/OAuth doğrulaması production smoke aşamasında yapılacaktır.

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Final WEB acceptance **A → B → C → D → E → F → G** sırasıyla kapatılacaktır.
