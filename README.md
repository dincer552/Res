# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas C. Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

---

## GERÇEK ÇALIŞMA SIRASI

Motorlar artık birbirinden bağımsız paralel hesaplar olarak değil, aşağıdaki bağımlılık zinciriyle çalışıyor:

```text
M3 Player Analysis
      ↓
M4 Legal Formations
      ↓
M5 XI Candidates
      ↓
M6-A Global Search
      │
      ├─→ M7 Regional Rating
      │       ↓
      │     M7.2 Tactical Scenario
      │       ↓
      │     M8 Chance Allocation
      │       ↓
      │     M9 Event → Goal + W/D/L
      │
      ↓
DB1
  ↓
M10 Formation Competition / Rank
  ↓
M6-B Rank-Driven Refinement
  │
  └─→ her seed için tekrar M7 → M7.2 → M8 → M9
  ↓
DB2
  ↓
M11 Final Selector
  ↓
WEB
```

**Önemli:** M7/M7.2/M8/M9, M6-A'nın aday evaluator zincirinin downstream parçalarıdır. Yani `M6-A → M7 → M7.2 → M8 → M9` şeklinde bağımlıdır; M6 tamamlanmadan M10 başlamaz. M6-B ise M10 rank'ını kullanır ve kendi adaylarını yine aynı M7→M7.2→M8→M9 zincirinden geçirir.

---

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
| M9 | ✅ REGRESSION | Event→Goal + PNF/PDIM + symmetric opponent Specialty + C.1/C.2 utilities |
| Monte Carlo | ✅ REGRESSION | 18×5 dk + 5 senaryo + 1000 maç + deterministic seed + iki taraf event sampling |
| M10 | ✅ | Formation competition + MC W/D/L composite ranking |
| M6-B | ✅ | M10 formation rank → tiered beam/iteration budget |
| DB2 | ✅ | Formation diversity/depth korunuyor |
| M11 | ✅ | Offline full-pipeline final comparison |
| UI / Motor Panel | ✅ | M9 own/opponent event + set-piece + MC diagnostics |

---

## 03.09.2026 — SON GELİŞMELER

- [x] M9 Event → Goal breakdown genişletildi.
- [x] PNF extra-attack mekanizması eklendi.
- [x] PDIM normal-attack suppression eklendi.
- [x] Appendix C.1 set-piece goal probability utility eklendi.
- [x] Appendix C.2 Long Shot / tactic conversion curve engine'e taşındı.
- [x] Event contribution / expected-goal breakdown eklendi.
- [x] 18 × 5 dakikalık event-based Monte Carlo sampling eklendi.
- [x] MC'de own + opponent special-event contribution simetrik örnekleniyor.
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
- [x] Canonical full CHPP JSON (`TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json`) CI offline regression girdisi yapıldı.
- [x] Full CHPP JSON ile M3→M11 offline regression geçti.
- [x] Full CHPP JSON ile Docker build geçti.
- [x] Aynı HEAD Azure deployment + health check geçti.

### SIRADAKİ İŞLER — VERİYE BAĞLI OLANLAR

- [ ] Historical event + Long Shot calibration
- [ ] Set-piece taker skill → exact goal conversion calibration
- [ ] Specialty ↔ weather / tactic cross-effects
- [ ] Exact V5 tactic-level → paper RT mapping
- [ ] Historical event + çoklu gerçek-maç validation
- [ ] Final WEB release / production acceptance

---

## CI CHECKPOINT

**Son doğrulanmış HEAD:** `620fea9e41e2fafb1143f79d8fb89a12262a0bd3`

**Workflow:** HattrickAI V5 Deploy #500 — **SUCCESS**.

Run #500 içinde:

```text
M7-M8 Offline Regression       PASS
Full CHPP JSON                  PASS
Docker build                    PASS
Docker image upload             PASS
Azure deployment                PASS
Container health check          PASS
```

Böylece önceki `CS0246 / CS1503 / CS7036` compile zinciri artık current HEAD için regression'ı bloklamıyor.

---

## FULL CHPP JSON — GİRDİ KONTROLÜ

CI artık eski küçük `s4msunfc-m7-m8.json` fixture'ına değil, gerçek CHPP export'una benzeyen canonical dosyaya karşı çalışıyor:

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Dosyanın mevcut V5 offline pipeline için gerekli çekirdeği sağladığı **gerçek çalıştırmayla doğrulandı**:

```text
normalized.ownPlayers
        ↓
v5Analysis.ownLineup
        ↓
v5Analysis.opponentRating
        ↓
M3 → M4 → M5 → M6-A
        ↓
M7 → M7.2 → M8 → M9
        ↓
M10 → M6-B → DB2 → M11
```

Ayrıca raw CHPP export içinde oyuncu skill/specialty ve `SetPiecesSkill` gibi M9 için kullanılan veriler mevcut. JSON'da credential/OAuth/session bilgileri dahil edilmemiştir.

**Sonuç:** JSON, mevcut offline V5 zincirini çalıştırmak için yeterli. Ancak production calibration için hâlâ çoklu tarihsel maç/event sonuçları, gözlemlenemeyen set-piece taker conversion ilişkisi, weather cross-effect ve V5 tactic-level→paper RT eşlemesi gerekiyor. Veri yokken katsayı uydurulmayacak.

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

Beş tactic için paper'daki regression curves M7.2 → M8 handoff'unda aktif kullanılıyor:

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

C.2 artık dead utility değil; aktif tactic conversion diagnostic/handoff değeridir. `RT` değerinin V5 tactic-level ile birebir eşlemesi ise hâlâ calibration problemidir.

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

Acceptance:

```text
1000 simulations             PASS
W/D/L sum = 1                PASS
score distribution           PASS
most likely score            PASS
5 scenarios                  PASS
scenario total = 1000       PASS
deterministic repeat         PASS
opponent events symmetric    PASS
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

## UYGULAMA SIRASI

```text
1. Current HEAD CI + Azure deployment          ✅
2. Full CHPP JSON offline regression            ✅
3. Motor dependency/order validation            ✅ CODED + regression
4. Historical event + Long Shot calibration     ⏳ DATA
5. Set-piece taker exact calibration            ⏳ DATA
6. Specialty ↔ weather/tactic cross-effects     ⏳ DATA / weather input
7. Exact V5 tactic-level → paper RT mapping      ⏳ CALIBRATION
8. CHPP / real-match multi-match validation      ⏳ DATA
9. Final WEB production acceptance               ⏳
```

### Kabul kriteri

Bir iş yalnızca kodlandığı için `✅` yapılmaz. Şu üç aşama ayrı tutulur:

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Bu README her commit'te gerçek durumun kaydı olarak güncellenir.
