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
C13 DB2 formation coverage           🟢 ACCEPTED
C14 M11 finalist pool                 ⏳
C15 M11 final selection               ⏳
C16 FinalPlan continuity              ⏳
C17 FinalPrediction continuity        ⏳
C18 deterministic rerun               ⏳
```

### Audit status — C1 → C13

- **C13 DB2 formation coverage:** gerçek `MotorPipelineService` çalıştırılıyor ve production'da oluşan DB2 doğrudan acceptance result üzerinden görünür hale getiriliyor. Regression; DB2'nin kapasite sınırını, yalnızca gerçek `M6-B` kayıtlarından oluşmasını, duplicate candidate içermemesini, geçerli XI/score/chance değerlerini ve M4'ün tüm legal formasyonlarının DB2'de korunmasını doğruluyor. Fixture-specific sonuç hard-code edilmiyor.

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
M11 Final Selection
      ↓
FinalPlan / FinalPrediction
```

### Kabul kriteri

```text
CODED       → mekanizma production kodunda var
REGRESSION  → güncel production pipeline'a bağlı offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

**C1–C13 audit/regression çalışmaları tamamlandı.** Sıradaki iş **C14 M11 finalist pool**.
