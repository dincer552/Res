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
| Monte Carlo | 🔧 | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed; M9 event wrapper entegrasyonu düzeltildi |
| M10 | 🔧 | Formation competition + MC W/D/L composite ranking mevcut; CI/E2E doğrulaması bekleniyor |
| M6-B | 🔧 | Formation-aware budget altyapısı mevcut; M10 rank'ının bütçeye doğrudan taşınması sırada |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | 🔧 | MC + tactical + structural + stability/risk score ile final selector mevcut; E2E doğrulaması bekleniyor |
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
- [x] M9 regression artık Tables 4–5 + Appendix C.1/C.2 + PNF + PDIM mekanizmalarını kontrol ediyor.
- [x] M9 `EventGoals` downstream wrapper tarafından kaybolduğunda nested `MatchPrediction.EventGoals` fallback'i eklendi.
- [x] M10 `RankedCandidate` compile blocker düzeltildi.
- [x] UI M9 / MC paneli güncellendi.
- [x] UI nested/full prediction sonucunu tercih edecek şekilde düzeltildi.

### SIRADAKİ İŞLER

- [ ] CI green checkpoint
- [ ] M9 opponent Specialty event wiring
- [ ] Long Shot scoring graph historical calibration
- [ ] Set-piece taker hidden-skill input
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] V5 tactic level → paper RT exact mapping
- [ ] M10 formation ranking end-to-end validation
- [ ] M6-B M10 rank → refinement budget entegrasyonu
- [ ] M11 end-to-end regression
- [ ] Historical event + real-match validation
- [ ] Final WEB release

---

## CI CHECKPOINT

Son doğrulanmış kırmızı workflow eski committeydi:

```text
Commit: 70265c056ec6a769658858e011d8a579465304d3
Job: 100125726900
M10FinalDecisionEngine.cs(33,30)
CS0246: RankedCandidate could not be found
```

Compile blocker düzeltildi:

```text
431cfcdc5c58ac4666f9d7160cd1ce7b27ea3dd7
```

Ardından M9 integration/regression değişiklikleri yapıldı:

```text
cce3a5748a51bfc4929c648b820040ce1b8cba6b
35d0ef5336a2bb456fb6915790e7d70b9885e8a6
9d3af6031cf4a971e64bfccb19b110265b8b5291
```

En son CI durumu şu anda **pending**; yeşil checkpoint henüz doğrulanmış değil.

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
PNF conversion
1 PNF → CD 0/1/2/3 = 9.6% / 6.9% / 3.3% / 2.0%
2 PNF → CD 0/1/2/3 = 11.7% / 9.6% / 5.2% / 3.1%
PNF ≥3             = 6.6%

PDIM suppression
1 PDIM ≈ 6.5% normal-attack suppression
```

PNF, normal fırsatların kaçan kısmından extra attack üretir; PDIM normal attack volume'ünü baskılar. Bu mekanizmalar için regression fixture eklendi.

### Appendix C

```text
C.1 ATTACKSP(d)
= -0.0000380429·d³ + 0.0000226846·d² + 0.0366246·d + 0.45515

C.2 TCR_LS(RT)
= 0.00761935·RT + 0.07520052
```

Utilities regression ile kontrol ediliyor. Ancak hidden/set-piece taker skill, historical LS scoring graph ve exact V5→RT mapping kanıtlanmadan production coefficient'i olarak uydurulmuyor.

### M9 production integration

M9 downstream pipeline bazı noktalarda `M9PredictionResult` nesnesini yeniden oluşturuyor. Bu wrapper'ın `EventGoals` alanı boş olsa bile event katkılarının kaybolmaması için `M9PredictionResult.EventGoals` artık nested `Prediction.EventGoals` değerine fallback yapıyor. Böylece simulation/UI aynı canonical event çıktısını görür.

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

Simulation deterministic seed kullanır ve score frequency + W/D/L + scenario breakdown üretir. M9 event contribution katmanı simulation'a taşınır; PNF ve own-goal double-count edilmez.

---

## M10 — FORMATION COMPETITION

M10 ranking modeli:

```text
normalized Tactical score
+ Monte Carlo Win probability
+ Structural score
↓
Composite score
↓
Formation leaderboard
```

Her legal formasyon DB1'de depth ile korunur. M10 formation competition artık MC W/D/L bilgilerini de taşır. Kalan iş, CI ve gerçek fixture üzerinden end-to-end doğrulamadır.

---

## M6-B — REFINEMENT

Mevcut M6 motoru formation bazında ayrı search pass çalıştırabilir ve `M6FormationSearchBudget` ile formasyon başına beam width / iteration tanımlayabilir. `preserveInputOrders=true` durumunda refinement budget formasyon ranking'ine göre kademelenebiliyor.

Kalan üretim işi:

```text
M10 formation rank
      ↓
formation budget
      ↓
M6-B search depth
      ↓
DB2
```

Amaç güçlü formasyonlara daha fazla arama, zayıf formasyonlara daha düşük ama sıfır olmayan keşif bütçesi vermektir.

---

## M11 — FINAL SELECTOR

M11 mevcut final scoring:

```text
35% tactical
35% MC win
15% structural
 5% stability
10% risk-adjusted outcome
```

Final seçim için DB2'de tüm legal formasyonlar korunur. Kalan acceptance kriteri uçtan uca regression'dır.

---

## UI / MOTOR PANELİ

UI artık aşağıdaki motor çıktılarının tanısal görünümünü verir:

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
2. M9 production event integration         ← kod düzeltmesi yapıldı; CI doğrulaması bekliyor
3. Historical event + LS calibration
4. M10 formation ranking validation
5. M6-B M10-rank-driven refinement
6. M11 end-to-end regression
7. CHPP / real-match validation
8. Final WEB release
```

Her gerçek kod değişikliğinden sonra bu README güncellenecek. Bir motor yalnızca kodu mevcut diye `✅` yapılmayacak; regression / integration acceptance kriteri de geçilecek.
