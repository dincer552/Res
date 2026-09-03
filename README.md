# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas C. Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

---

## ANA MOTOR ZİNCİRİ

```text
M3 → M4 → M5 → M6-A / DB1 → M7 → M7.2 → M8 → M9
                                              ↓
                                     18 × 5 min Monte Carlo
                                              ↓
                                             W/D/L
                                              ↓
                                            M10
                                              ↓
                                           M6-B / DB2
                                              ↓
                                            M11
                                              ↓
                                            WEB
```

## MOTOR DURUMU

| Motor | Durum | Gerçek durum |
|---|---|---|
| M3 | ✅ | Skill + position/order/side + form/experience/loyalty + Specialty |
| M4 | ✅ | Legal formasyon havuzu |
| M5 | ✅ | Geniş XI candidate üretimi |
| M6-A | ✅ | Formation-aware global search + DB1 |
| M7 | ✅ | Bölgesel gerçek ratingler |
| M7.2 | ✅ | PDF tactical mechanisms + canonical handoff |
| M8 | ✅ | Chance allocation + tactic opportunity layer |
| M9 | 🔧 | Event→Goal, PNF, PDIM, CA/LS ve Appendix C utilities mevcut; opponent/hidden inputs ve historical calibration eksik |
| Monte Carlo | 🔧 | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed; offline regression geçti |
| M10 | ✅ | Formation competition + MC W/D/L composite ranking; full offline regression geçti |
| M6-B | 🔧 | Formation-aware refinement çalışıyor; M10 rank → budget bağlantısı güçlendirilecek |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | ✅ | Offline full-pipeline regression'da tüm legal formasyonlarla final comparison geçti |
| UI / Motor Panel | ✅ | M9 event breakdown + MC + scenario görünümü mevcut |

---

## 03.09.2026 — SON GELİŞMELER

- [x] M9 Event → Goal breakdown genişletildi.
- [x] PNF extra-attack mekanizması eklendi.
- [x] PDIM normal-attack suppression eklendi.
- [x] Appendix C.1 set-piece goal probability utility eklendi.
- [x] Appendix C.2 Long Shot conversion utility eklendi.
- [x] Event contribution / expected-goal breakdown eklendi.
- [x] 18 × 5 dakikalık event-based Monte Carlo sampling eklendi.
- [x] 5 MC senaryosu ve 1000-match deterministic yapı korundu.
- [x] PNF double-count engellendi.
- [x] M9 regression fixture stabil baseline'a geri çekildi.
- [x] M9 regression Tables 4–5 + Appendix C.1/C.2 + PNF + PDIM mekanizmalarını kontrol ediyor.
- [x] M9 `EventGoals` downstream wrapper kaybına karşı nested `MatchPrediction.EventGoals` fallback'i eklendi.
- [x] M10 `RankedCandidate` compile blocker düzeltildi.
- [x] M10 full offline formation leaderboard validation geçti.
- [x] M11 full offline end-to-end final comparison geçti.
- [x] UI M9 / MC paneli güncellendi.
- [x] UI nested/full prediction sonucunu tercih edecek şekilde düzeltildi.
- [x] CI offline regression ve Docker build başarıyla geçti; deploy zinciri doğrulaması devam ediyor.

### SIRADAKİ İŞLER — SIRA BOZULMADAN

- [ ] Full CI green + Azure deployment checkpoint
- [ ] M9 opponent Specialty event wiring
- [ ] Long Shot scoring graph historical calibration
- [ ] Set-piece taker hidden-skill input
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] V5 tactic level → paper RT exact mapping
- [ ] M6-B M10 rank → refinement budget entegrasyonu
- [ ] Historical event + real-match validation
- [ ] Final WEB release

---

## CI CHECKPOINT

Son workflow:

```text
Workflow: HattrickAI V5 Deploy #472
Run: 33787883833
Head: 4beef3b406d038db7ce66e0590be600839ea9201

M7-M8 Offline Regression: PASS
Docker Build: PASS
Current: deploy pipeline continues
```

Daha önceki compile blocker:

```text
70265c056ec6a769658858e011d8a579465304d3
M10FinalDecisionEngine.cs(33,30)
CS0246: RankedCandidate could not be found
```

Düzeltme:

```text
431cfcdc5c58ac4666f9d7160cd1ce7b27ea3dd7
```

Bu checkpoint'te offline regression artık yeşil. Full CI, image upload ve Azure health/deploy sonucu tamamlanmadan `CI ✅` ilan edilmiyor.

---

## M9 — EVENT → GOAL

PDF Tables 4–5 baseline event sınıfları doğrudan kodlandı:

```text
Winger
Technical over Head
Quick Rush / Quick Pass
Unpredictable Long Pass / Score Own / Special Action / Mistake / Own Goal
Corner
Experienced Forward
Inexperienced Defender
Tired Defender
```

Baseline:

```text
Player events = Binomial(n=4, p=0.841)
Team events   = Binomial(n=5, p=0.372)
```

### PNF / PDIM

```text
PNF
1 PNF → CD 0/1/2/3 = 9.6% / 6.9% / 3.3% / 2.0%
2 PNF → CD 0/1/2/3 = 11.7% / 9.6% / 5.2% / 3.1%
PNF ≥3             = 6.6%

PDIM
1 PDIM ≈ 6.5% normal-attack suppression
```

### Appendix C

```text
C.1 ATTACKSP(d)
= -0.0000380429·d³ + 0.0000226846·d² + 0.0366246·d + 0.45515

C.2 TCR_LS(RT)
= 0.00761935·RT + 0.07520052
```

### Production'da hâlâ veri isteyenler

```text
Opponent Specialty detail
Set-piece taker hidden skill
Long Shot scoring graph / conversion calibration
Specialty ↔ weather / tactic cross-effects
V5 tactic level → paper RT exact mapping
```

Bu alanlarda veri yokken katsayı uydurulmayacak.

---

## EVENT-BASED MONTE CARLO

```text
90 min
 ↓
18 × 5 min tick
 ↓
Normal chance sampling
 ↓
M9 event sampling
 ↓
Event → goal
 ↓
Final score
 ↓
1000-match W/D/L distribution
```

Offline regression ile şu acceptance kriterleri geçti:

```text
1000 simulations
W/D/L sum = 1
score distribution mevcut
most likely score mevcut
5 scenario mevcut
scenario total = 1000
deterministic repeat mevcut
```

M9 event contribution wrapper fallback'i de regression zincirinde çalışıyor.

---

## M10 — FORMATION COMPETITION

M10 ranking:

```text
normalized Tactical score
+ Monte Carlo Win probability
+ Structural score
↓
Composite score
↓
Formation leaderboard
```

Offline regression her legal formasyonun leaderboard'da bulunmasını, depth status'unu ve finite composite score'u doğruluyor.

**M10 acceptance geçti.** Bundan sonraki teknik iş M6-B arama bütçesinin M10 rank'ına doğrudan bağlanmasıdır.

---

## M6-B — REFINEMENT

Mevcut M6 engine formation bazlı search pass ve `M6FormationSearchBudget` destekliyor. `preserveInputOrders=true` ile DB1 seed'leri korunuyor ve rank tabanlı tier mantığı mevcut.

Son entegrasyon hedefi:

```text
M10 formation rank
      ↓
rank tier / search budget
      ↓
M6-B beam width + iteration
      ↓
DB2
```

Güçlü formasyon daha fazla refinement bütçesi almalı; zayıf formasyon minimum keşif bütçesini korumalıdır. Legal formasyon tamamen silinmemelidir.

---

## M11 — FINAL SELECTOR

M11 scoring:

```text
35% tactical
35% MC win
15% structural
 5% stability
10% risk-adjusted outcome
```

Offline regression:

```text
all legal formations reach DB2
all legal formations reach M11
ranking non-empty
winner final score finite
deterministic finalist
```

**M11 offline acceptance geçti.**

---

## UI / MOTOR PANELİ

```text
Analitik tahmin
W / D / L
Expected goals
Possession
7 rating / position matchup

M9 Event → Goal
Player event xG
Team event xG
PNF xG
CA xG
Long Shot xG
Own Goal xG
PDIM suppression
Calibration status
Event contributions

Monte Carlo
1000 matches
18 ticks
Most likely score
W / D / L
Scenario distribution
```

---

## BURADAN İTİBAREN UYGULAMA SIRASI

```text
1. Full CI + Azure deployment checkpoint       ← ŞİMDİ
2. M9 opponent Specialty event wiring
3. Historical event + LS calibration
4. M6-B M10-rank-driven refinement budget
5. M11 production/real-match regression
6. CHPP / real-match validation
7. Final WEB release
```

Not: M10 ve M11 offline acceptance geçti; bu yüzden artık tekrar aynı regression kodunu yazmak yerine kalan production/data entegrasyonlarına geçiyoruz.

---

## CALIBRATION KURALI

```text
Verified mechanism
      ↓
production baseline
      +
CHPP historical data
      ↓
residual/error analysis
      ↓
confidence test
      ↓
production adjustment
```

Tek maç veya küçük örneklem sonucu doğrudan production katsayısına çevrilmez.
