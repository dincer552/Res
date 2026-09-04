# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-04

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas C. Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131. DOI: `10.1016/j.entcom.2026.101131`.

## GERÇEK ÇALIŞMA SIRASI

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
      ↓
DB1 → M10 → M6-B → DB2 → M11 → WEB
```

**Önemli:** M7/M7.2/M8/event-goal zinciri M6-A ve M6-B candidate evaluator'larının downstream parçalarıdır.

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
| Historical Calibration Engine | ✅ CODED + REGRESSION |
| Set-piece taker calibration | ✅ CODED + REGRESSION |
| Specialty ↔ weather / tactic | ✅ CODED + REGRESSION |
| V5 tactic-level → paper RT mapping | ✅ CODED + REGRESSION |

## 04.09.2026 — SPECIALTY INTERACTION TAMAMLANDI

`SpecialtyInteractionEngine` ile aşağıdaki mekanizmalar kodlandı ve offline regression'a bağlandı:

- Technical / Powerful / Quick weather etkileri (%5 skill etkisi)
- Quick oyuncuların Counter Attack tactic-level bonusu ve rakip Quick savunmacı azaltması
- Technical Defensive Forward passing etkisi
- Powerful oyuncuların Pressing savunma ağırlığı
- Technical defender/wing-back non-tactical counterattack sinyali
- Head specialty corner/set-piece etkisi
- Creative special-event amplification

Hattrick'in geliştirici dokümantasyonu Technical/Powerful/Quick weather etkilerini %5, Quick CA bonusunu ise 1 ekstra Quick için %5 ve 8 için %14'e kadar tanımlar. V5, kaynakta açıkça bulunmayan ara katsayıları production gerçeği gibi sunmaz; calibration gerektiğinde ayrı tutulur.

## HISTORICAL CALIBRATION

Historical calibration katmanı gerçek CHPP gözlemlerini paper baseline ile karşılaştırmak üzere ayrı tutulur.

Production activation gate:

```text
>= 250 eligible matches
AND
>= 250 Long Shot attempts
AND
regression comparison
```

Historical fit production katsayılarını otomatik değiştirmez.

## SET-PIECE TAKER

Set-piece taker skill için calibration engine ve regression mevcut. Taker seçimi ve gözlem corpus'u hazırdır; gerçek hidden-game-engine conversion katsayısı yeterli matched historical CHPP/event verisi gelmeden production katsayısı olarak aktive edilmez.

## PAPER M8 / TACTIC CONVERSION

Paper Appendix C.2'de tactic conversion rate, tactic rating `RT` üzerinden Equation B.2 ile tanımlanır:

```text
Counter:
-0.617941717072569 + 0.104274398·RT
-0.00358354796·RT² + 0.0000434356·RT³

AiM:
-0.00036765·RT² + 0.02180462·RT + 0.0705084

AoW:
-0.00046569·RT² + 0.02894608·RT + 0.10514706

Long Shot:
0.00761935·RT + 0.07520052

Pressing:
-0.00780421·RT² + 0.471402·RT - 1.10735
```

V5 artık M8 içinde linear `min/max` interpolation yerine bu **paper Equation B.2 curves**'ünü kullanıyor.

`TacticPaperMappingEngine` açık bir ölçek köprüsü olarak V5'in mevcut **0–10 internal tactical scale**'ini paper'ın kullanılan **0–20 tactic-skill/RT aralığına** taşır:

```text
V5 0  → RT 0
V5 5  → RT 10
V5 10 → RT 20
```

M8'deki mevcut taktik çağrıları da bu bridge üzerinden geçer. Böylece V5 tarafındaki taktik seviyesi doğrudan paper Equation B.2'ye beslenmez; önce RT'ye dönüştürülür. Paper'ın Table 8'inde CA ve LS için 20 tactic skill örneği kullanıldığı için üst sınır 20 olarak sabitlenmiştir.

Bu adım **CODED + REGRESSION** olarak tamamlanmıştır. Gerçek CHPP çoklu maçlarıyla nihai production kabulü ayrı calibration/acceptance aşamasıdır.

## PAPER CHANCE BASELINE

Paper-derived production baseline:

```text
Eq.1  possession
Eq.2  5 exclusive + 5 shared
Eq.3  L/M/R + set-piece distribution
C.1   set-piece scoring regression
C.2   tactic conversion curves
```

Paper'ın 1 milyon maçlık datasetinde tactic/tactic skill, sector ratings, midfield, ISP ratings ve specialty sayıları input olarak kullanılır.

## FULL CHPP JSON — OFFLINE ACCEPTANCE

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Offline zincirde M3→M11 akışının çalışması regression ile doğrulanır.

## CI CHECKPOINT

Son doğrulanmış production workflow:

```text
HattrickAI V5 Deploy #505  → SUCCESS

Offline regression   PASS
Docker build         PASS
Docker image upload  PASS
Azure deployment     PASS
Health check         PASS
```

Yeni specialty / tactic mapping değişiklikleri CI'da ayrıca doğrulanmaktadır.

## KALANLAR

```text
1. Set-piece taker skill → exact goal conversion       ✅ CODED + REGRESSION / PRODUCTION DATA
2. Specialty ↔ weather / tactic cross-effects          ✅ CODED + REGRESSION
3. Exact V5 tactic-level → paper RT mapping             ✅ CODED + REGRESSION
4. Historical multi-match production acceptance         ⏳ DATA
5. Final WEB production acceptance                      ⏳
```

Buradaki `⏳ DATA` maddeleri kod eksikliği değildir; gerekli çoklu gerçek CHPP/event corpus veya hidden game-engine değişkenleri olmadan güvenilir production katsayısı üretilemez.

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Tactic RT mapping artık **CODED + REGRESSION** seviyesinde tamamlanmıştır; production acceptance çoklu gerçek CHPP verisiyle #4 altında yürütülecektir.
