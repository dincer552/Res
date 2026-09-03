# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

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
| M9 | 🔧 | Event→Goal, PNF, PDIM, CA/LS ve Appendix C utilities mevcut; bazı production girdileri eksik |
| Monte Carlo | 🔧 | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed mevcut; event fallback düzeltildi, calibration eksik |
| M10 | 🔧 | Formation competition + MC W/D/L composite ranking mevcut; CI doğrulaması bekleniyor |
| M6-B | 🔧 | DB1 seed/refinement mevcut; rank-driven depth geliştirilecek |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | 🔧 | MC + tactical + structural + stability/risk score ile final selector mevcut |
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
- [x] UI M9 / MC paneli güncellendi.
- [x] UI nested/full prediction sonucunu tercih edecek şekilde düzeltildi.
- [x] M10 `RankedCandidate` compile blocker düzeltildi.
- [x] MC katmanında wrapper EventGoals boş kaldığında `Prediction.EventGoals` fallback'i eklendi; M9 event çıktısı simulation içinde kaybolmayacak.

### SIRADAKİ İŞLER

- [ ] CI green checkpoint
- [ ] M9 opponent Specialty event wiring
- [ ] Long Shot scoring graph historical calibration
- [ ] Set-piece taker hidden-skill input
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] V5 tactic level → paper RT exact mapping
- [ ] M10 formation ranking end-to-end validation
- [ ] M6-B rank-driven search depth
- [ ] M11 end-to-end regression
- [ ] Historical event + real-match validation
- [ ] Final WEB release

---

## CI CHECKPOINT

En son kırmızı CI eski commit üzerinde şu hatayla durmuştu:

```text
Commit: 70265c056ec6a769658858e011d8a579465304d3
Job: 100125726900
M10FinalDecisionEngine.cs(33,30)
CS0246: RankedCandidate could not be found
```

Bu hata giderildi:

```text
Fix commit: 431cfcdc5c58ac4666f9d7160cd1ce7b27ea3dd7
```

Ardından M9 Monte Carlo event kaybı için:

```text
Fix commit: cce3a5748a51bfc4929c648b820040ce1b8cba6b
```

Yeni CI sonucu henüz alınmadığı için **CI ✅ ilan edilmiyor**.

---

## M9 — EVENT → GOAL

PDF Tables 4–5 baseline event sınıfları:

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

Appendix C:

```text
C.1 ATTACKSP(d)
= -0.0000380429·d³ + 0.0000226846·d² + 0.0366246·d + 0.45515

C.2 TCR_LS(RT)
= 0.00761935·RT + 0.07520052
```

PNF ve PDIM ayrı mekanizmalar olarak uygulanır. Opponent Specialty, hidden set-piece skill, LS scoring graph ve exact RT mapping veriyle doğrulanmadan uydurulmaz.

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

Önemli entegrasyon düzeltmesi: `M9PredictionResult.EventGoals` boş bir wrapper üzerinden okunuyorsa simulation artık `MatchPrediction.EventGoals` değerine geri düşüyor. Böylece pipeline'ın M9 çıktıyı yeniden sarmalaması event katkısını yok etmiyor.

---

## M10 — FORMATION COMPETITION

M10 composite ranking:

```text
Tactical score
+ Monte Carlo Win probability
+ Structural score
↓
Formation leaderboard
```

Her legal formasyonun DB1 depth'i korunur. M10'un MC verisini production'da doğru kullanması yeni CI + end-to-end regression ile doğrulanacaktır.

---

## M6-B — REFINEMENT

```text
DB1
 ↓
M10 formation ranking
 ↓
M6-B seeds
 ↓
refinement search
 ↓
DB2
```

Mevcut M6-B tüm DB1 seed'lerini formation diversity korunarak işler. Bir sonraki geliştirme M10 sırasını arama bütçesine çevirecek: güçlü formasyon daha fazla refinement, zayıf formasyon minimum keşif bütçesi alacak; hiçbir legal formasyon tamamen kilitlenmeyecek.

---

## M11 — FINAL SELECTOR

M11 DB2 finalistlerini son kez değerlendirir:

```text
35% tactical
35% MC win
15% structural
 5% stability
10% risk-adjusted outcome
```

Final kararın acceptance kriteri: tüm legal formasyonların DB2 → M11 zincirinde korunması, deterministic ranking ve end-to-end regression.

---

## UI / MOTOR PANELİ

UI'da görünen temel motor çıktıları:

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
1. CI / compile temizliği                  ← ŞİMDİ
2. M9 production event integration
3. Historical event + LS calibration
4. M10 formation ranking validation
5. M6-B rank-driven depth refinement
6. M11 end-to-end regression
7. CHPP / real-match validation
8. Final WEB release
```

Her gerçek kod değişikliğinden sonra README'deki durum bu sıraya göre güncellenecek; tamamlanmayan motor `✅` yapılmayacak.

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

Tek maç veya küçük örneklem sonucu doğrudan motor katsayısı olarak kullanılmaz.
