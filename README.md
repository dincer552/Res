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

`HistoricalMultiMatchProductionAcceptance` offline suite içine bağlıdır. Kabul eşiği artık 60 gerçek CHPP-türetilmiş maçtır:

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

M8 Phase D collector tarafından üretilen `hattrickai-v5-m8-phase-d-calibration-v2` JSON'u da acceptance gate tarafından doğrudan okunur. Bu veri setinde 60 sample, 60 source row, 60 detail fetch ve 60 chance sample bulunur; katsayılar bu kabul testi sırasında değiştirilmez.

Web tarafındaki geniş collector hâlâ daha büyük corpus üretebilir; ancak mevcut proje kabul aşamasını kapatmak için **60 maçlık CHPP-derived corpus** yeterlidir.

`observedOwnSetPieceChances` sample alanı bu corpus'ta boş/null kalmıştır; buna rağmen raw-derived `sourceRows` içinde home/away special-event chance alanları mevcuttur. Bu durum acceptance'i bloke etmez ve set-piece taker katsayılarını değiştirmez.

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

Offline zincirde M3→M11 akışının çalışması regression ile doğrulanır. Historical acceptance gate eski export'u geriye dönük uyumlulukla kontrol eder; Phase D calibration corpus ise `hattrickai-v5-m8-phase-d-calibration-v2` şemasıyla 60 maçlık acceptance yolundan geçer.

## CI CHECKPOINT

Son doğrulanmış baseline:

```text
HattrickAI V5 Deploy #505  → SUCCESS
```

60-maç acceptance değişikliği, specialty ve tactic mapping regression'ları CI'da doğrulanmaktadır.

## KALANLAR

```text
1. Set-piece taker skill → exact goal conversion       ✅ CODED + REGRESSION / PRODUCTION DATA
2. Specialty ↔ weather / tactic cross-effects          ✅ CODED + REGRESSION
3. Exact V5 tactic-level → paper RT mapping             ✅ CODED + REGRESSION
4. Historical multi-match production acceptance         ✅ 60-MATCH CHPP CALIBRATION ACCEPTED
5. Final WEB production acceptance                      ⏳
```

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

#4 için 60-maç collector + acceptance gate hazır ve Phase D CHPP-derived corpus ile kabul edildi. Sıradaki gerçek adım: **Final WEB production acceptance.**
