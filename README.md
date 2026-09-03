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
                                  M10 rank-driven M6-B
                                              ↓
                                           DB2 → M11
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
| M9 | 🔧 | Event→Goal + PNF/PDIM + symmetric opponent Specialty wiring + Appendix C utilities; historical calibration eksik |
| Monte Carlo | 🔧 | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed; M9 event contribution entegre |
| M10 | ✅ | Formation competition + MC W/D/L composite ranking; offline acceptance geçti |
| M6-B | ✅ | M10 formation rank → tiered beam/iteration budget doğrudan pipeline'a bağlandı; DB2 diversity korunuyor |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | ✅ | Offline full-pipeline final comparison geçti |
| UI / Motor Panel | ✅ | M9 event / opponent event / set-piece input / MC diagnostics gösteriliyor |

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
- [x] M9 opponent tarafı için CHPP oyuncu roster'ı ve son resmi maç lineup'ı `OpponentMatchProfile` içine bağlandı.
- [x] M9 opponent Specialty event hesabı artık rakip lineup + Specialty verisi mevcutsa çalışıyor.
- [x] CHPP `SetPiecesSkill` oyuncu modeline alındı ve M9 set-piece taker diagnostics'e taşındı.
- [x] M10 `RankedCandidate` compile blocker düzeltildi.
- [x] M10 full offline formation leaderboard validation geçti.
- [x] M11 full offline end-to-end final comparison geçti.
- [x] M10 formation rank → M6-B beam/iteration budget entegrasyonu pipeline'a bağlandı.
- [x] M6-B seed sırası M10 formation rank'a göre düzenlendi.
- [x] M6-B her formation için minimum pozitif refinement bütçesini koruyor.
- [x] UI M9 / MC paneli güncellendi.
- [x] UI opponent event contributions + set-piece taker skill gösteriyor.

### SIRADAKİ İŞLER — SIRA BOZULMADAN

- [ ] Current HEAD CI green + Azure deployment checkpoint
- [ ] Long Shot scoring graph historical calibration
- [ ] Set-piece taker skill → exact goal conversion calibration
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] V5 tactic level → paper RT exact mapping
- [ ] Historical event + real-match validation
- [ ] Final WEB release

---

## CI CHECKPOINT

Son doğrulanmış workflow'da offline regression geçti; yeni M9 opponent/set-piece değişiklikleri için HEAD CI yeniden doğrulanıyor.

```text
M7-M8 Offline Regression: PASS
M9 Event/Goal regression: PASS
M10 formation leaderboard regression: PASS
M11 final comparison regression: PASS
M9 1000× Monte Carlo regression: PASS
Docker Build: PASS
```

Önceki compile blocker:

```text
70265c056ec6a769658858e011d8a579465304d3
M10FinalDecisionEngine.cs(33,30)
CS0246: RankedCandidate could not be found
```

Düzeltme:

```text
431cfcdc5c58ac4666f9d7160cd1ce7b27ea3dd7
```

**Full Azure deployment green sonucu, mevcut HEAD için ayrıca doğrulanmadan `CI ✅` ilan edilmiyor.**

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

### Opponent Specialty wiring

Opponent CHPP rosterı artık `OpponentMatchProfile.Players`, son resmi maçın final 11'i ise `LastMatchLineup` olarak taşınıyor. M9 aynı event engine'i rakip perspektifinden de çalıştırıyor:

```text
Opponent lineup + Specialty
        ↓
Opponent M9 events
        ↓
Opponent PNF / PDIM / event goals
        ↓
Own normal-volume suppression + opponent event xG
```

Böylece M9 artık yalnızca kendi takımının Specialty'lerine bakmıyor. Opponent roster/lineup yoksa fallback olarak eski one-sided davranış korunuyor.

### Set-piece taker input

CHPP `SetPiecesSkill` Player modeline taşındı. Mevcut M9, final XI içindeki en yüksek set-piece skill'i taker diagnostic olarak işaretliyor. Bu değer henüz Appendix C.1 fonksiyonuna ek bir katsayı olarak uygulanmıyor; bunun için doğrulanmış historical relationship gerekiyor.

### Appendix C

```text
C.1 ATTACKSP(d)
= -0.0000380429·d³ + 0.0000226846·d² + 0.0366246·d + 0.45515

C.2 TCR_LS(RT)
= 0.00761935·RT + 0.07520052
```

### Production'da hâlâ calibration isteyenler

```text
Long Shot scoring graph / conversion
Set-piece taker skill → exact conversion
Specialty ↔ weather / tactic cross-effects
V5 tactic level → paper RT exact mapping
Historical event coefficients
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
M9 own + opponent event sampling
 ↓
Event → goal
 ↓
Final score
 ↓
1000-match W/D/L distribution
```

Offline regression ile:

```text
1000 simulations
W/D/L sum = 1
score distribution mevcut
most likely score mevcut
5 scenarios mevcut
scenario total = 1000
deterministic repeat mevcut
```

Own ve opponent event katkıları M9 prediction'a taşınıyor; PNF ve own-goal double-count korunuyor.

---

## M10 — FORMATION COMPETITION

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

**M10 acceptance geçti.**

---

## M6-B — M10 RANK-DRIVEN REFINEMENT

```text
M10 rank
   ↓
Tier 1 → daha geniş beam + daha fazla iteration
Tier 2 → orta bütçe
Tier 3 → daha küçük ama sıfır olmayan bütçe
   ↓
M6-B formation search
   ↓
DB2
```

M10 rank doğrudan M6-B budget üretimine giriyor. Seed'ler de rank sırasına göre düzenleniyor. Legal formasyon anti-lock nedeniyle tamamen silinmiyor.

**M10 → M6-B entegrasyonu kodlandı ve pipeline'a bağlandı.**

---

## M11 — FINAL SELECTOR

```text
35% tactical
35% MC win
15% structural
 5% stability
10% risk-adjusted outcome
```

Offline acceptance:

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
Biz oyuncu event xG
Biz takım event xG
PNF / PDIM / CA / LS / Own Goal
Set-piece taker skill

Opponent Event → Goal
Rakip oyuncu event xG
Rakip takım event xG
Rakip PNF / PDIM / Own Goal
Event contribution tabloları

Monte Carlo
1000 matches
18 ticks
Most likely score
W / D / L
Scenario distribution
```

M10 formation competition, MC win probability'yi karar sinyali olarak kullanıyor; UI aynı sonucu tanısal detaylarla gösteriyor.

---

## BURADAN İTİBAREN UYGULAMA SIRASI

```text
1. Current HEAD CI + Azure deployment green checkpoint  ← ŞİMDİ
2. Historical event + Long Shot calibration
3. Set-piece taker exact calibration
4. Specialty ↔ weather/tactic cross-effects
5. Exact V5 tactic-level → paper RT mapping
6. CHPP / real-match validation
7. Final WEB release
```

Her gerçek kod değişikliğinden sonra README aynı sırayı ve gerçek acceptance durumunu yansıtacak. Kodlandı ama doğrulanmadıysa `🔧`, regression geçtiyse `✅` olarak kalacak.
