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
| Historical multi-match acceptance gate | 🟡 HARNESS + DATA VALIDATION |

## HISTORICAL MULTI-MATCH PRODUCTION ACCEPTANCE

`HistoricalMultiMatchProductionAcceptance` artık offline suite içine bağlıdır. Gate şu kontrolleri yapar:

```text
CHPP export schema/source doğrulaması
finished match filtreleme
benzersiz MatchID kontrolü
final skor bütünlüğü
multi-match kapsamı
ayrıntılı CHPP match-engine kaydı kontrolü
```

Production activation için mevcut eşik:

```text
>= 250 finished matches / side
AND
>= 250 detailed CHPP match-engine records
AND
HistoricalCalibrationEngine gate:
    >= 250 eligible matches
    >= 250 Long Shot attempts
    regression comparison
```

Mevcut `HattrickAI_V5_CHPP_FullOffline_2026-09-01.json` export'unda 8 own + 8 opponent finished match bulunuyor ve ayrıntılı match-engine verisi bulunan tek reference match mevcut. Bu nedenle acceptance harness çalışır durumda olsa da **production calibration henüz aktive edilmez**; veri yetersizliği bilinçli olarak gate tarafından `DATA_INCOMPLETE` olarak bırakılır.

Bu ayrım önemlidir: gerçek CHPP verisiyle sadece sonuç listesini görmek, rating/tactic-skill girişlerini her tarihsel maç için bilmeden gerçek predictive acceptance anlamına gelmez. Veri sızıntısını önlemek için pre-match gözlenen rating/tactic inputs olmadan geçmiş maç üzerinde tahmin başarısı hesaplanmaz.

## 04.09.2026 — SPECIALTY INTERACTION TAMAMLANDI

`SpecialtyInteractionEngine` ile aşağıdaki mekanizmalar kodlandı ve offline regression'a bağlandı:

- Technical / Powerful / Quick weather etkileri (%5 skill etkisi)
- Quick oyuncuların Counter Attack tactic-level bonusu ve rakip Quick savunmacı azaltması
- Technical Defensive Forward passing etkisi
- Powerful oyuncuların Pressing savunma ağırlığı
- Technical defender/wing-back non-tactical counterattack sinyali
- Head specialty corner/set-piece etkisi
- Creative special-event amplification

## PAPER M8 / TACTIC CONVERSION

Paper Appendix C.2'de tactic conversion rate, tactic rating `RT` üzerinden Equation B.2 ile tanımlanır. V5 bu eğrileri M8 içinde kullanır ve tactic scale bridge regression ile korunur.

## PAPER CHANCE BASELINE

```text
Eq.1  possession
Eq.2  5 exclusive + 5 shared
Eq.3  L/M/R + set-piece distribution
C.1   set-piece scoring regression
C.2   tactic conversion curves
```

Paper çalışması 250 değişkenli 1 milyon CHPP maçlık veri kümesi kullandı; tactic/tactic skill, sector ratings, midfield, ISP ratings ve specialty sayıları gözlenen girdiler arasında yer aldı. Tahmin değerlendirmesinde gol farkı hatası ve HDA için RPS kullanıldı. fileciteturn676file0

## FULL CHPP JSON — OFFLINE ACCEPTANCE

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Offline zincirde M3→M11 akışının çalışması regression ile doğrulanır. Historical acceptance gate aynı export'un geçmiş maç listesini ayrıca kontrol eder.

## CI CHECKPOINT

Son doğrulanmış baseline:

```text
HattrickAI V5 Deploy #505  → SUCCESS
```

Yeni historical acceptance, specialty ve tactic mapping değişiklikleri CI'da doğrulanmaktadır.

## KALANLAR

```text
1. Set-piece taker skill → exact goal conversion       ✅ CODED + REGRESSION / PRODUCTION DATA
2. Specialty ↔ weather / tactic cross-effects          ✅ CODED + REGRESSION
3. Exact V5 tactic-level → paper RT mapping             ✅ CODED + REGRESSION
4. Historical multi-match production acceptance         🟡 HARNESS + DATA REQUIRED
5. Final WEB production acceptance                      ⏳
```

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

#4 için kod ve acceptance gate hazırdır. **Production kabulü için gereken tarihsel multi-match CHPP corpus henüz yeterli değildir.**