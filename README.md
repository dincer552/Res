# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 hedefi tek bir ilk 11 bulmak değil; **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → formation competition → refinement → final selector → WEB** zincirini tek ve tutarlı bir motor olarak çalıştırmaktır.

Makale referansı: Anthony C. Constantinou, Nicholas Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

---

## ANA MOTOR ZİNCİRİ

```text
M3 Player Profile
    ↓
M4 Legal Formations
    ↓
M5 XI Candidates
    ↓
M6-A Global Search + DB1
    ↓
M7 Real Rating
    ↓
M7.2 Tactical Mechanisms
    ↓
M8 Chance Engine
    ↓
M9 Event → Goal
    ↓
18 × 5 min Event-based Monte Carlo
    ↓
W / D / L
    ↓
M10 Formation Competition
    ↓
M6-B Refinement + DB2
    ↓
M11 Final Selector
    ↓
WEB / Motor Panel
```

## MOTOR DURUMU

| Motor | Durum | Gerçek durum |
|---|---|---|
| M3 | ✅ | Oyuncu skill + position/order/side + form/experience/loyalty + Specialty taşınıyor |
| M4 | ✅ | Legal formasyon havuzu korunuyor |
| M5 | ✅ | Formasyon başına geniş XI adayı üretimi mevcut |
| M6-A | ✅ | Formation-aware global search + DB1 diversity mevcut |
| M7 | ✅ | L/C/R defence + midfield + L/C/R attack ratingleri mevcut |
| M7.2 | ✅ | PDF taktik mekanikleri ve canonical handoff mevcut |
| M8 | ✅ | Chance allocation + tactic opportunity katmanı mevcut |
| M9 | 🔧 | Event→goal + PNF/PDIM + CA/LS + Appendix C utilities mevcut; üretim girdilerinin tamamı henüz bağlı değil |
| Monte Carlo | 🔧 | 18×5 dk, 5 senaryo, 1000 maç ve deterministic seed mevcut; historical/event calibration bekliyor |
| M10 | 🔧 | Formation competition + MC W/D/L ranking mevcut; son CI doğrulaması gerekiyor |
| M6-B | 🔧 | DB1 seed/refinement mevcut; rank-driven depth daha da güçlendirilecek |
| DB2 | ✅ | Formation depth ve diversity korunuyor |
| M11 | 🔧 | Final selector MC/tactical/structural sinyallerini kullanıyor; son uçtan uca doğrulama bekliyor |
| UI / Motor Panel | ✅ | M9 event breakdown, PNF/PDIM ve MC çıktıları gösteriliyor |

---

## 03.09.2026 — TAMAMLANAN Geliştirmeler

- [x] M9 Event → Goal breakdown genişletildi.
- [x] PNF (Powerful Normal Forward) extra-attack mekanizması eklendi.
- [x] PDIM normal-attack suppression mekanizması eklendi.
- [x] Appendix C.1 set-piece goal probability utility eklendi.
- [x] Appendix C.2 Long Shot tactic conversion utility eklendi.
- [x] M9 event contribution / expected-goal breakdown eklendi.
- [x] 18 × 5 dakikalık event-based Monte Carlo sampling eklendi.
- [x] Base / Sol kanat / Sağ kanat / Düşük şans / Yüksek şans senaryoları korundu.
- [x] 1000-match deterministic simulation yapısı korundu.
- [x] PNF event-goal katkısının MC'de double-count edilmesi engellendi.
- [x] M9 offline regression fixture stabil baseline'a geri çekildi.
- [x] UI M9 paneli yeni Event → Goal / MC çıktılarıyla güncellendi.
- [x] UI simulation kaynağı nested/full prediction sonucunu kullanacak şekilde düzeltildi.
- [x] UI event contribution tablosu + PNF/PDIM/CA/LS/Own Goal diagnostics gösterimi eklendi.
- [x] M10 `RankedCandidate` compile blocker düzeltildi; tip kullanımını class scope'unun başına taşıdık.

### HENÜZ TAMAMLANMAYAN

- [ ] CI green checkpoint
- [ ] M9 opponent Specialty event wiring
- [ ] Long Shot scoring graph historical calibration
- [ ] Set-piece taker hidden-skill integration
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] V5 tactic level → paper RT exact mapping
- [ ] M10 MC sonuçlarının formation ranking kararına production'da tam bağlanmasının doğrulanması
- [ ] M6-B rank-driven search depth refinement
- [ ] M11 end-to-end regression
- [ ] Historical event calibration + gerçek maç validation

---

## SON CI CHECKPOINT

En son kırmızı workflow eski commit üzerinde şurada durdu:

```text
Commit: 70265c056ec6a769658858e011d8a579465304d3
Workflow job: 100125726900
Failure: HattrickAI_V5/Core/M10FinalDecisionEngine.cs(33,30)
Error: RankedCandidate could not be found
```

Bu hata için M10'daki nested `RankedCandidate` tanımı methodların üstüne taşındı.

```text
Fix commit: 431cfcdc5c58ac4666f9d7160cd1ce7b27ea3dd7
Durum: yeni CI doğrulaması bekleniyor
```

**Bu nedenle CI şu anda README'de yeşil ilan edilmiyor.**

---

## M9 — EVENT → GOAL

PDF Tables 4–5 baseline event sınıfları kodlandı:

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

Baseline event budget:

```text
Player events = Binomial(n=4, p=0.841)
Team events   = Binomial(n=5, p=0.372)
```

Appendix C utilities:

```text
C.1 ATTACKSP(d)
= -0.0000380429·d³
  + 0.0000226846·d²
  + 0.0366246·d
  + 0.45515

d = ISP attack − ISP defence

C.2 TCR_LS(RT)
= 0.00761935·RT + 0.07520052
```

PNF:

```text
1 PNF → CD 0/1/2/3 = 9.6% / 6.9% / 3.3% / 2.0%
2 PNF → CD 0/1/2/3 = 11.7% / 9.6% / 5.2% / 3.1%
PNF ≥3             = 6.6%
```

PDIM baseline:

```text
1 PDIM ≈ 6.5% normal-attack suppression
```

### M9'da eksik girdiler

Bunlar veri yokken uydurulmayacak. Önce input modeli ve historical validation kurulacak:

```text
Opponent Specialty detail
Set-piece taker hidden skill
Long Shot scoring graph / conversion calibration
Specialty ↔ weather / tactic cross-effects
V5 tactic level → paper RT exact mapping
```

---

## EVENT-BASED MONTE CARLO

90 dakika artık tek bir maçlık Poisson çekişi olarak ele alınmıyor. Simulation katmanı:

```text
90 min
 ↓
18 × 5 min tick
 ↓
Normal chance sampling
 ↓
Event eligibility
 ↓
Event occurrence
 ↓
Event → goal conversion
 ↓
Final score
```

Korunan özellikler:

```text
1000 matches
5 scenarios
deterministic seed
W / D / L
score distribution
most-likely score
```

MC'nin production'a tam yükseltilmesi için opponent Specialty ve historical event/Long Shot calibration tamamlanmalıdır.

---

## M10 — FORMATION COMPETITION

M10 artık yalnızca tactical score bakmıyor. Formation competition için:

```text
Tactical score
+ Monte Carlo Win probability
+ Structural score
↓
Composite score
↓
formation leaderboard
```

Her legal formasyon için candidate depth korunur. MC çıktısı formation ranking'e bağlanmıştır; yeni CI regression ile bunun uçtan uca stabil olduğu doğrulanacaktır.

---

## M6-B — REFINEMENT

M6-B DB1'den gelen seed'lerle ikinci arama yapar ve DB2 oluşturur.

Mevcut korumalar:

```text
DB1 → M10 competition
DB1 → M6-B seeds
M6-B → DB2
DB2 → M11
```

Her legal formasyon için minimum depth korunur. Bir sonraki geliştirme, M10 formation ranking sonucunu M6-B arama derinliğine doğrudan yön vermektir; güçlü formasyon daha fazla refinement bütçesi almalı, zayıf formasyon ise tamamen silinmeden daha düşük bütçeyle devam etmelidir.

---

## M11 — FINAL SELECTOR

M11 DB2 finalistlerini son kez karşılaştırır:

```text
35% tactical
35% MC win
15% structural
 5% stability
10% risk-adjusted outcome
```

Final çıktı:

```text
Best Formation
Best XI
Best Rating
Best Matchup
MC W / D / L
Most likely score
```

M11'in tamamlanma kriteri yalnızca derlenmek değildir; gerçek pipeline regression ile seçilen finalist ve tüm legal formationların DB2 → M11 zincirinde korunması gerekir.

---

## UI / MOTOR PANELİ

Panelde artık:

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

MC şu aşamada tanısal/kanıt çıktısıdır. M10 ranking'e bağlanmış mekanizma mevcut olsa da production checkpoint'i için CI + end-to-end regression beklenmektedir.

---

## UYGULAMA SIRASI — BURADAN DEVAM

```text
1. CI / compile temizliği                       ← ŞİMDİ
2. M9 production event integration
3. Historical event + LS calibration
4. M10 formation ranking validation
5. M6-B rank-driven depth refinement
6. M11 end-to-end regression
7. Real-match / CHPP validation
8. Final WEB release
```

Bu sıra tamamlanmadan yeni bir motoru `✅ tamamlandı` ilan etmiyoruz.

---

## CALIBRATION KURALI

```text
Verified paper mechanism
        ↓
production baseline
        +
CHPP historical data
        ↓
residual/error analysis
        ↓
confidence test
        ↓
only then → production coefficient update
```

Tek maç sonucu veya küçük örneklem üzerinden motor eğilmeyecek.
