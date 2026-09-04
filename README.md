# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-04

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas C. Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

---

## GERÇEK ÇALIŞMA SIRASI

Motorlar bağımsız paralel hesaplar olarak değil, aşağıdaki bağımlılık zinciriyle çalışıyor:

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
      │     event → goal + W/D/L
      │
      ↓
DB1
  ↓
M10 Formation Competition / Rank
  ↓
M6-B Rank-Driven Refinement
  │
  └─→ her seed için tekrar M7 → M7.2 → M8 → event/goal
  ↓
DB2
  ↓
M11 Final Selector
  ↓
WEB
```

**Önemli:** M7/M7.2/M8/event-goal zinciri M6-A'nın candidate evaluator'ının downstream parçalarıdır. M10, M6-A/DB1 tamamlandıktan sonra çalışır. M6-B, M10 rank'ını kullanır ve kendi adaylarını tekrar aynı evaluator zincirinden geçirir.

---

## MOTOR DURUMU

| Motor | Durum |
|---|---|
| M3 | ✅ |
| M4 | ✅ |
| M5 | ✅ |
| M6-A | ✅ |
| M7 | ✅ |
| M7.2 | ✅ |
| M8 | ✅ |
| Event → Goal / W-D-L | ✅ REGRESSION |
| Monte Carlo | ✅ REGRESSION |
| M10 | ✅ |
| M6-B | ✅ |
| DB2 | ✅ |
| M11 | ✅ |
| UI / Motor Panel | ✅ |
| **Historical Calibration Engine** | **✅ CODED + REGRESSION** |

---

## 04.09.2026 — HISTORICAL CALIBRATION TAMAMLANDI

Historical calibration artık ayrı bir kod katmanı olarak tamamlandı:

```text
HistoricalCalibrationEngine
        ↓
HatStats >= 333 filtresi
        ↓
CHPP match observations
        ↓
sector / event / Long Shot ölçümü
        ↓
paper baseline ile hata karşılaştırması
        ↓
activation gate
```

### Kalibrasyon motorunun ölçtükleri

- Normal L/M/R toplam chance hacmi
- L/M/R sektör payları
- Paper baseline'a signed error
- Published special-event occurrence + goal-rate referansları
- Historical special-event occurrence / goal-rate
- Long Shot attempts / goals
- Tactic rating'e göre gözlenen Long Shot conversion
- Paper C.2 Long Shot curve'üne MAE

### Production güvenlik kuralı

Historical fit otomatik olarak production katsayısını değiştirmez. Aday tarihsel kalibrasyonun production'a geçebilmesi için:

```text
>= 250 eligible matches
AND
>= 250 Long Shot attempts
AND
regression comparison
```

Bu gate, tek bir fixture veya küçük sample'ın maç motorunu bozmasını önler.

### Araştırma makalesi baseline'ı

Paper'daki tarihsel 1 milyon maçlık modelden yayımlanan referans oranlar kod içinde immutable baseline olarak tutulur:

```text
Winger                  21.63%   goal .4951
Technical over Head     12.77%   goal .2937
Quick Rush              12.86%   goal .3670
Quick Pass              12.19%   goal .4387
Unpred Long Pass         6.87%   goal .4090
Unpred Score Own         5.36%   goal .5822
Unpred Special Action    5.60%   goal .4241
Unpred Mistake            2.90%  goal .1816
Unpred Own Goal           3.92%  goal .1725
Experienced Forward       4.00%  goal .3704
Inexperienced Defender   3.92%  goal .1050
Tired Defender             .04%  goal .3432
Corner                   29.22%  goal .4849
```

Paper, bu hyperparameter'ların historical data ile öğrenildiğini ve event modelinin Normal-vs-Normal + ilgili speciality'lerin mümkün olduğu maçlara göre kurulduğunu açıklar. fileciteturn523file0

### Long Shot

Paper Appendix C.2'de Long Shot dönüşümü:

```text
TCR(RT) = 0.00761935·RT + 0.07520052
```

Historical calibration engine gerçek CHPP gözlemlerini bu eğriyle karşılaştırır; yeterli tarihsel veri olmadan eğriyi yeniden yazmaz. fileciteturn523file0

### Mevcut 60-maç CHPP doğrulaması

Daha önceki 60 gerçek CHPP maçı, PDF chance mimarisinin ayrıca doğrulandığını gösteriyor:

```text
Observed L/M/R total = 8.80 / match
Paper expectation      = 8.745 / match
```

Ve possession/chance ownership tarafında paper Eq.1, daha önceki basit/regression yaklaşımlarından daha düşük hata vermişti. Bu nedenle production chance çekirdeği paper mekanizmasına bağlı tutuluyor.

---

## PAPER MEKANİZMASI

Paper 1 milyon match / 250 variable dataset kullanıyor; minimum HatStats eşiği 333. Inputlarda sector ratings, midfield, ISP ratings, tactic/tactic skill ve speciality sayıları bulunuyor. fileciteturn523file0

Production'da kullanılan çekirdek:

```text
Eq.1  possession
Eq.2  5 exclusive + 5 shared
Eq.3  L/M/R + set-piece distribution
Eq.4  attack vs defence scoring
C.1   set-piece regression utility
C.2   tactic conversion curves
```

C.2 curve'leri dead utility değil; tactic opportunity/handoff hesaplarında kullanılıyor. Exact V5 tactic-level → paper RT eşlemesi ise ayrı bir calibration problemi olarak korunuyor.

---

## FULL CHPP JSON — OFFLINE ACCEPTANCE

Canonical regression girdisi:

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Gerçek offline zincirde gerekli core data bulunduğu ve M3→M11 akışının geçtiği doğrulandı.

---

## CI CHECKPOINT

Current branch HEAD: `v5`

Son doğrulanmış workflow:

```text
HattrickAI V5 Deploy #505  → SUCCESS

Offline regression   PASS
Docker build         PASS
Docker image upload  PASS
Azure deployment     PASS
Health check         PASS
```

---

## KALANLAR

Historical calibration katmanı tamamlandı. Bundan sonra gerçek production doğrulaması için kalan işler:

```text
1. Set-piece taker skill → exact goal conversion       ⏳ DATA
2. Specialty ↔ weather / tactic cross-effects          ⏳ DATA
3. Exact V5 tactic-level → paper RT mapping             ⏳ CALIBRATION
4. Historical multi-match production acceptance         ⏳ DATA
5. Final WEB production acceptance                      ⏳
```

Buradaki `⏳ DATA` maddeleri kod eksikliği değildir; gerekli çoklu gerçek CHPP/event corpus veya hidden game-engine değişkenleri olmadan güvenilir katsayı üretilemez.

---

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Historical calibration için engine + regression artık `CODED + REGRESSION` seviyesindedir; production activation yalnızca yeterli gerçek historical corpus geldiğinde yapılır.
