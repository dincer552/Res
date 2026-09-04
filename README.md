# HattrickAI V5

## FINAL WEB PRODUCTION ACCEPTANCE

Aktif branch: `v5`.

V5 hedefi: oyuncu → formasyon → XI → M6-A → M7 → M7.2 → M8 → M9 → DB1 → M10 → M6-B → DB2 → M11 → FinalPlan / FinalPrediction → WEB zincirini tek ve tutarlı maç motoru olarak çalıştırmak.

### Acceptance planı

```text
C1  M3 input/output continuity       🟢 ACCEPTED
C2  M4 legal formations              🟢 ACCEPTED
C3  M5 XI candidates                 🟢 ACCEPTED
C4  M6-A + evaluator chain           🟢 ACCEPTED
C5  M7 regional rating               🟢 ACCEPTED
C6  M7.2 tactical scenario           🟢 ACCEPTED
C7  M8 chance model                  🟢 ACCEPTED
C8  M9 prediction                    🟢 ACCEPTED
C9  DB1 formation coverage           🟢 ACCEPTED
C10 M10 formation competition        🟢 ACCEPTED
C11 M10 → M6-B rank-driven handoff   🟢 ACCEPTED
C12 M6-B refinement                  🟢 ACCEPTED
C13 DB2 formation coverage            🟢 ACCEPTED
C14 M11 finalist pool                 🟢 ACCEPTED
C15 M11 final selection               🟢 ACCEPTED
C16 FinalPlan continuity              🟡 REGRESSION ADDED
C17 FinalPrediction continuity        ⏳
C18 deterministic rerun               ⏳
```

### Audit status — C1 → C16

- **C15 M11 final selection:** gerçek `MotorPipelineService` çalıştırılıyor ve M11 final selector'ın DB2 finalistleri arasından deterministik seçim yaptığı doğrulanıyor. Regression; finalist/ranking sayısının korunmasını, final skorlarının finite ve azalan sırada olmasını, duplicate candidate bulunmamasını, legal formasyon çeşitliliğini, Winner = Ranking #1 ilişkisini, kazananın doğrudan DB2/M6-B kaynağından gelmesini, M9 prediction continuity'yi ve M6-B → M11 telemetry sırasını doğruluyor. Fixture-specific winner hard-code edilmiyor.
- **C16 FinalPlan continuity:** gerçek `MotorPipelineService` çıktısındaki `FinalPlan` ile M11 `BestPlan` birebir süreklilik açısından karşılaştırılıyor; formasyon, XI oyuncu/slot imzası, rating, matchup ve tactical score korunuyor. `FinalPrediction` da M11 prediction ile aynı nesne/değer olarak doğrulanıyor. Final XI'nin 11 benzersiz oyuncu ve 11 benzersiz slot içermesi ayrıca kontrol ediliyor.

### Güncel production zinciri

```text
M3 Player Analysis
      ↓
M4 Legal / Feasible Formations
      ↓
M5 XI Candidates (20 / formation)
      ↓
M6-A Global Search
      └─ gerçek evaluator: M7 → M7.2 → M8 → M9
      ↓
Candidate DB #1
      ↓
M10 Formation Competition
      ↓
M10 rank-driven budgets
      ↓
M6-B Refinement  ← C12 acceptance
      └─ gerçek evaluator: M7 → M7.2 → M8 → M9
      ↓
Candidate DB #2  ← C13 formation coverage
      ↓
M11 Final Selection  ← C15 acceptance
      ↓
FinalPlan  ← C16 continuity
      ↓
FinalPrediction  ← C17
```

### Kabul kriteri

```text
CODED       → mekanizma production kodunda var
REGRESSION  → güncel production pipeline'a bağlı offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

**C1–C15 audit/regression çalışmaları tamamlandı. C16 regression eklendi; sıradaki iş C16'nın güncel CI/production regression koşusunda PASS edilmesi, ardından C17 FinalPrediction continuity.**