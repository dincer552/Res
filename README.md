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
| M8 | ✅ | Chance allocation + tactic opportunity + C.2 tactic conversion handoff |
| M9 | 🔧 | Event→Goal + PNF/PDIM + symmetric opponent Specialty + C.1/C.2 utilities; historical calibration eksik |
| Monte Carlo | 🔧 | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed; event-aware |
| M10 | ✅ | Formation competition + MC W/D/L composite ranking; offline acceptance geçti |
| M6-B | ✅ | M10 formation rank → tiered beam/iteration budget doğrudan pipeline'a bağlandı |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | ✅ | Offline full-pipeline final comparison geçti |
| UI / Motor Panel | ✅ | M9 own/opponent event + set-piece + MC diagnostics gösteriliyor |

---

## 03.09.2026 — SON GELİŞMELER

- [x] M9 Event → Goal breakdown genişletildi.
- [x] PNF extra-attack mekanizması eklendi.
- [x] PDIM normal-attack suppression eklendi.
- [x] Appendix C.1 set-piece goal probability utility eklendi.
- [x] Appendix C.2 Long Shot / tactic conversion curve engine'e taşındı.
- [x] Event contribution / expected-goal breakdown eklendi.
- [x] 18 × 5 dakikalık event-based Monte Carlo sampling eklendi.
- [x] 5 MC senaryosu ve 1000-match deterministic yapı korundu.
- [x] PNF double-count engellendi.
- [x] M9 opponent CHPP roster + historical final XI entegrasyonu eklendi.
- [x] Opponent Specialty event resolution simetrik hale getirildi.
- [x] CHPP `SetPiecesSkill` Player modeline taşındı.
- [x] M10 formation leaderboard offline acceptance geçti.
- [x] M10 formation rank → M6-B budget entegrasyonu kodlandı.
- [x] M6-B seed sırası M10 rank'a bağlandı.
- [x] M11 full offline final comparison acceptance geçti.
- [x] UI own/opponent event diagnostics ve MC görünümü güncellendi.
- [x] M9 wrapper compatibility compile blocker düzeltildi.

### SIRADAKİ İŞLER — SIRA BOZULMADAN

- [ ] Current HEAD CI green + Azure deployment checkpoint
- [ ] Historical event + Long Shot calibration
- [ ] Set-piece taker skill → exact goal conversion calibration
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] Exact V5 tactic-level → paper RT mapping
- [ ] Historical event + real-match validation
- [ ] Final WEB release

---

## CI CHECKPOINT

Son doğrulanmış workflow sonucu: önceki run'da M7–M8 offline regression build hatası nedeniyle durdu; sonraki commit'ler bu compile zincirini düzeltti ve yeni HEAD için doğrulama yeniden çalıştırılıyor.

Önceki kritik hatalar:

```text
CS0246: M9CalibrationStatus could not be found
CS1503: MatchPrediction → M9PredictionResult conversion
CS7036: M9PredictionResult constructor argument mismatch
```

Bu nedenle README yalnızca gerçekten doğrulanmış testleri `✅` kabul eder. **Current HEAD CI + Azure deploy henüz green ilan edilmedi.**

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

```text
Opponent CHPP players + last official final XI
                    ↓
             Specialty event engine
                    ↓
          PNF / PDIM / event goals
                    ↓
        normal-volume suppression + xG
```

Opponent roster/lineup yoksa eski fallback korunuyor.

### Set-piece taker

CHPP `SetPiecesSkill` Player modeline taşındı ve M9 diagnostics'e açıldı. Paper, taker skill'inin gizli olması nedeniyle bunun doğrudan gözlemlenebilir historical coefficient'ini sağlamıyor; bu nedenle değer **diagnostic input** olarak tutuluyor, uydurma conversion katsayısı uygulanmıyor.

### Appendix C.1

```text
ATTACKSP(d)
= -0.0000380429·d³ + 0.0000226846·d² + 0.0366246·d + 0.45515
```

### Appendix C.2

Beş tactic için paper'daki regression curves M7.2 → M8 handoff'unda kullanılacak şekilde bağlandı:

```text
Counter
-0.617941717072569 + 0.104274398·RT - 0.00358354796·RT² + 0.0000434356·RT³

AiM
-0.00036765·RT² + 0.02180462·RT + 0.0705084

AoW
-0.00046569·RT² + 0.02894608·RT + 0.10514706

Long Shot
0.00761935·RT + 0.07520052

Pressing
-0.00780421·RT² + 0.471402·RT - 1.10735
```

C.2 artık yalnızca dead utility değil; aktif tactic conversion diagnostic/handoff değeridir. Buna rağmen `RT` değerinin V5 tactic-level ile birebir eşlemesi hâlâ ayrı bir calibration problemidir.

### Hâlâ veri isteyen production alanları

```text
Historical event calibration
Long Shot scoring graph calibration
Set-piece taker skill → exact conversion
Specialty ↔ weather/tactic cross-effects
V5 tactic level → paper RT exact mapping
```

Veri yokken katsayı uydurulmayacak.

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

Acceptance hedefleri:

```text
1000 simulations
W/D/L sum = 1
score distribution mevcut
most likely score mevcut
5 scenarios mevcut
scenario total = 1000
deterministic repeat mevcut
```

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

Offline acceptance ile her legal formasyonun leaderboard'a taşındığı, depth status ve finite composite score korunuyor.

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

M10 rank doğrudan budget sözleşmesine ve seed sırasına bağlandı. Anti-lock korunuyor.

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

---

## UI / MOTOR PANELİ

```text
Analitik tahmin
W / D / L
Expected goals
Possession
7 rating / position matchup

M9 own Event → Goal
M9 opponent Event → Goal
PNF / PDIM / CA / LS / Own Goal
Set-piece taker skill
Event contribution tables
Calibration status

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
1. Current HEAD CI + Azure deployment green checkpoint  ← ŞİMDİ
2. Historical event + Long Shot calibration
3. Set-piece taker exact calibration
4. Specialty ↔ weather/tactic cross-effects
5. Exact V5 tactic-level → paper RT mapping
6. CHPP / real-match validation
7. Final WEB release
```

### Kabul kriteri

Bir iş yalnızca kodlandığı için `✅` yapılmaz. Şu üç aşama ayrı tutulur:

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Bu README her commit'te gerçek durumun kaydı olarak güncellenir.
