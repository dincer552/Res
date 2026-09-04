# HattrickAI V5

## FINAL WEB PRODUCTION ACCEPTANCE

Aktif branch: `v5`.

V5 hedefi: oyuncu → formasyon → XI → M6-A → M7 → M7.2 → M8 → M9 → DB1 → M10 → M6-B → DB2 → M11 → FinalPlan / FinalPrediction → WEB zincirini tek ve tutarlı maç motoru olarak çalıştırmak.

### Önceden tamamlanan / korunacak çalışmalar

Aşağıdaki çalışmalar bu acceptance fazından önce tamamlandı ve yeni test fazında geriye alınmayacaktır:

- M6 global search / M6-B için production hazırlığı ve offline doğrulama altyapısı.
- M6 validation: legal matrices, Normal baseline, duplicate/player/formation consistency ve combination-count kontrolleri.
- M6 offline JSON doğrulaması: gerçek fixture üzerinde 3-5-2 / 11-slot aday ve davranış matrisi kontrolleri.
- WEB input integrity (A) regression.
- Core ↔ WEB parity (B) regression.
- Historical calibration regression ve 60-match production acceptance.
- Set-piece taker skill → exact goal conversion calibration/regression.
- Specialty ↔ weather/tactic cross-effects regression.
- V5 tactic-level → paper RT mapping regression.
- M8 paper TCR eğrileri ve exact paper TCR regression.
- Long Shot opportunity regression/fix.
- M9 event-goal regression.
- M4 legal formation regression altyapısı.
- M5 XI candidate regression altyapısı.
- M6-A candidate evaluation regression altyapısı.

Bu maddeler "yeni acceptance zincirinin yapılacak işleri" değil, mevcut V5 production mekanizmasının daha önce tamamlanmış temelleridir.

## C) M3 → M11 CURRENT PIPELINE ACCEPTANCE

Önemli kural: Offline testler eski mimarinin varsayımlarını değil, **mevcut `MotorPipelineService` production davranışını** kabul kriteri olarak kullanacaktır. Her assertion için:

1. ilgili production kodundaki gerçek davranış kontrol edilir,
2. assertion'ın eski mimariden kalıp kalmadığı denetlenir,
3. mevcut mimarinin gerçek acceptance criterion'u belirlenir,
4. yalnızca bundan sonra regression'a alınır.

### Acceptance planı

```text
C1  M3 input/output continuity       🟢 ACCEPTED
C2  M4 legal formations              🟢 ACCEPTED
C3  M5 XI candidates                 🟢 ACCEPTED
C4  M6-A + evaluator chain           🔄 AUDIT + REWRITE
C5  M7 regional rating               🟢 CODED + REGRESSION WIRED
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

### Audit status — C1 → C5

- **C1 M3:** audit + rewrite tamamlandı. Test artık gerçek `PlayerAnalysisEngine` çıktısını ve gerçek `MotorPipelineService` içindeki M3 continuity'yi doğruluyor; fixture-only JSON shape assertion kaldırıldı.
- **C2 M4:** audit + registry tamamlandı. Production `FormationCandidateEngine.LegalFormations` tek source of truth; test artık duplicate slot dizilerini tutmuyor ve production registry ile M4 çıktısını karşılaştırıyor.
- **C3 M5:** mevcut production `GenerateCandidates(..., maxCandidatesPerFormation: 20)` davranışıyla uyumlu; regression kabul edildi.
- **C4 M6-A:** yalnızca M6 sonuçlarının değil, güncel M6-A callback içindeki gerçek **M7 → M7.2 → M8 → M9 evaluator zincirinin** çalıştığı kanıtlanacak.
- **C5 M7:** gerçek `MotorPipelineService` çalıştırılıyor; seçilen FinalPlan XI'ı üzerinden M7 doğrudan production `RegionalRatingScenarioEngine` ile yeniden hesaplanıyor ve 7 bölgesel rating + raw değerler, MatchState, Team Spirit ve Coach Style modifier'ları karşılaştırılıyor.

### Güncel production zinciri

```text
M3 Player Analysis
      ↓
M4 Legal / Feasible Formations
      ↓
M5 XI Candidates (20 / formation)
      ↓
M6-A Global Search
      └─ callback içinde: M7 → M7.2 → M8 → M9
      ↓
Candidate DB #1
      ↓
M10 Formation Competition
      ↓
M10 rank-driven budgets
      ↓
M6-B Refinement
      └─ callback içinde: M7 → M7.2 → M8 → M9
      ↓
Candidate DB #2
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

Yeni çalışma sırası:

```text
C1 → C2 → C3 → C4 → C5 → C6 → C7 → C8
```

C1–C4 audit tamamlandı; C5 tamamlandı. Sıradaki iş C6 M7.2 → C7 M8 offline regression zinciridir.
