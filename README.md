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
| Historical multi-match acceptance gate | 🟡 HARNESS + DATA VALIDATION |

## HISTORICAL MULTI-MATCH PRODUCTION ACCEPTANCE

`HistoricalMultiMatchProductionAcceptance` offline suite içine bağlıdır. Gate şu kontrolleri yapar:

```text
CHPP export source/schema doğrulaması
finished match filtreleme
benzersiz MatchID kontrolü
final skor bütünlüğü
>=250 geçerli tarihsel maç
>=250 ayrıntılı match-engine gözlemi
invalid/error satırı kontrolü
```

Web arayüzündeki **📊 260 GEÇMİŞ MAÇI ÇEK + PRODUCTION JSON İNDİR** butonu artık CHPP `matchesarchive` üzerinden son 12 ayı 45 günlük pencerelerle tarar ve seçilen 260 bitmiş maç için `matchdetails` verisini de toplar. Detay istekleri arasında 5 saniye beklenir; amaç CHPP'ye burst trafik göndermemektir. CHPP `matchesarchive` tarih aralığında en fazla 50 maç döndürdüğü için arşiv birden fazla pencereye bölünür. citeturn2search0turn4search0

Butonun ürettiği JSON artık `hattrickai-v5-historical-production-v1` şemasındadır ve doğrudan acceptance gate'e verilebilecek yapıdadır. **Toplanan veri gözlem amaçlıdır; production katsayıları bu işlem sırasında değiştirilmez.**

Production acceptance için hedef:

```text
>= 250 valid finished matches
AND
>= 250 detailed CHPP match-engine observations
AND
0 invalid/error observations
```

CHPP `matchdetails` kaydı maç başına tactic skill, rating ve sonuç/chance gözlemlerini sağlar; bu nedenle yalnızca maç skorlarını çekmek yeterli değildir. citeturn1search0

Mevcut `HattrickAI_V5_CHPP_FullOffline_2026-09-01.json` export'u eski küçük corpus olduğu için production acceptance'i aktive etmez. Yeni butonla gerçek CHPP hesabından geniş corpus üretilecek.

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

Paper çalışması 250 değişkenli 1 milyon CHPP maçlık veri kümesi kullandı; tactic/tactic skill, sector ratings, midfield, ISP ratings ve specialty sayıları gözlenen girdiler arasında yer aldı. Tahmin değerlendirmesinde gol farkı hatası ve HDA için RPS kullanıldı.

## FULL CHPP JSON — OFFLINE ACCEPTANCE

```text
TestJSON/
└── HattrickAI_V5_CHPP_FullOffline_2026-09-01.json
```

Offline zincirde M3→M11 akışının çalışması regression ile doğrulanır. Historical acceptance gate eski export'u geriye dönük uyumlulukla kontrol eder; yeni production corpus ise `hattrickai-v5-historical-production-v1` şemasını kullanır.

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
4. Historical multi-match production acceptance         🟡 COLLECTOR READY — REAL DATA REQUIRED
5. Final WEB production acceptance                      ⏳
```

## KABUL KRİTERİ

```text
CODED       → mekanizma kodda var
REGRESSION  → offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

#4 için collector + UI butonu + acceptance gate hazırdır. Sıradaki gerçek adım: **siteye CHPP ile bağlanıp 260 maçlık JSON'u çekmek ve çıkan gerçek ölçümleri V5 tahminleriyle karşılaştırmak.**