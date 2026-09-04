# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-04

Aktif branch: `v5`

V5 hedefi: **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → M10 → M6-B → M11 → WEB** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmak.

Ana araştırma referansı: Anthony C. Constantinou, Nicholas C. Higgins, Neville K. Kitson — *Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning*, Entertainment Computing 57 (2026) 101131, DOI: `10.1016/j.entcom.2026.101131`.

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
| Historical multi-match acceptance gate | ✅ 60-MATCH CHPP CALIBRATION |

## HISTORICAL MULTI-MATCH ACCEPTANCE — 60 MAÇ

`HistoricalMultiMatchProductionAcceptance` offline suite içine bağlıdır. Kabul eşiği 60 gerçek CHPP-türetilmiş maçtır:

```text
CHPP / calibration schema doğrulaması
benzersiz MatchID kontrolü
final skor bütünlüğü
>=60 örnek
>=60 ayrıntılı source row
>=60 detail fetch
>=60 chance sample
0 failed detail
0 invalid sample/source row
```

M8 Phase D collector tarafından üretilen `hattrickai-v5-m8-phase-d-calibration-v2` JSON'u acceptance gate tarafından okunur. Bu veri setinde 60 sample, 60 source row, 60 detail fetch ve 60 chance sample bulunur; katsayılar bu kabul testi sırasında değiştirilmez.

`observedOwnSetPieceChances` sample alanı bu corpus'ta boş/null kalmıştır; raw-derived `sourceRows` içinde home/away special-event chance alanları mevcuttur. Bu durum acceptance'i bloke etmez ve set-piece taker katsayılarını değiştirmez.

## FINAL WEB PRODUCTION ACCEPTANCE

Bu aşamada yeni mekanizma geliştirmek yerine V5'in gerçek WEB sınırından Core motora ve tekrar WEB çıktısına kadar tek sözleşme olarak çalıştığı doğrulanır. Testler aşağıdaki sırayla ve birbirini geçtikten sonra ilerletilir:

```text
A) WEB input integrity             🟡 CI RERUN
B) Core ↔ WEB parity               🟡 IMPLEMENTED — CI PENDING
C) M3→M11 end-to-end               ⏳
D) Gerçek Hattrick match input     ⏳
E) prediction output               ⏳
F) regression suite                ⏳
G) production smoke test           ⏳

                ↓

          V5 WEB PRODUCTION
                ⏳
```

### A) WEB input integrity

`HattrickAI_V5.OfflineTests/WebInputIntegrityRegression.cs` offline suite'in ilk kontrolüdür. WEB questionnaire alanlarını, 14 saha slotunu, WEB → API endpoint bağlantılarını, session taşımasını, seçili maç ID'sini ve AnalysisService'in CHPP `teamdetails / training / players / matches / matchlineup / matchdetails` veri akışını statik sözleşme seviyesinde kontrol eder. Oyuncu tarafında 15 temel CHPP alanının map edildiği ve own/opponent 11 oyuncu bütünlüğünün korunduğu doğrulanır.

Canlı OAuth/CHPP erişimi A testinin parçası değildir; gerçek ortam doğrulaması G production smoke testinde yapılacaktır.

### B) Core ↔ WEB parity — AKTİF

`HattrickAI_V5.OfflineTests/CoreWebParityRegression.cs` eklendi ve offline suite'te A testinden hemen sonra çalıştırılıyor.

B'nin kabul ettiği sınır:

```text
Core Analysis object
        ↓
ASP.NET camelCase JSON serializer
        ↓
/api/v5/analysis
        ↓
WEB response bindings
```

Kontroller:

```text
analysis endpoint → Analysis object
camelCase JSON naming
build / team / opponent / match fields
own + opponent lineup
11 + 11 slot bütünlüğü
formation parity
ownRating / opponentRating
appliedQuestionnaire
regionalRatings / opponentThreat
WEB'in aynı canonical /api/v5/analysis response sözleşmesini tüketmesi
```

B testi canlı CHPP/OAuth yapmaz. Bu nedenle B PASS olsa bile production kabulü anlamına gelmez; canlı sınır G aşamasında doğrulanacaktır.

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

Paper çalışması 250 değişkenli 1 milyon CHPP maçlık veri kümesi kullandı; tactic/tactic skill, sector ratings, midfield, ISP ratings ve specialty sayıları gözlenen girdiler arasında yer aldı. Tahmin değerlendirmesinde gol farkı hatası ve HDA için RPS kullanıldı.

## FULL CHPP JSON — OFFLINE ACCEPTANCE

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Offline zincirde M3→M11 akışının çalışması regression ile doğrulanır. Historical acceptance gate eski export'u geriye dönük uyumlulukla kontrol eder; Phase D calibration corpus ise 60 maçlık CHPP-derived kabul yolundan geçer.

## CI CHECKPOINT

A ve B değişiklikleri `v5` branch'ine işlendi. B'nin CI sonucu henüz bekleniyor; CI temizlenmeden B production PASS olarak işaretlenmeyecektir.

## KALANLAR

```text
1. Set-piece taker skill → exact goal conversion       ✅ CODED + REGRESSION / PRODUCTION DATA
2. Specialty ↔ weather / tactic cross-effects          ✅ CODED + REGRESSION
3. Exact V5 tactic-level → paper RT mapping             ✅ CODED + REGRESSION
4. Historical multi-match production acceptance         ✅ 60-MATCH CHPP CALIBRATION ACCEPTED
5. Final WEB production acceptance                      🟡 A / B CI DOĞRULAMASI
```

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

Final WEB acceptance **A → B → C → D → E → F → G** sırasıyla kapatılacaktır.
