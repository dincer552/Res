# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5'nin hedefi yalnızca iyi bir ilk 11 bulmak değil; **oyuncu → formasyon → XI → rating → taktik → chance → event → goal → W/D/L → formasyon karşılaştırması → final** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmaktır.

2026 Hattrick araştırma makalesi M7.2/M8/M9 için ana araştırma referansıdır. Makale mekanizmaları production baseline olarak alınır; CHPP historical veri doğrulama ve kalibrasyon için kullanılır. Tek fixture veya küçük örneklem, güçlü bir araştırma mekanizmasının yerine otomatik geçirilemez.

## ANA V5 MOTOR MİMARİSİ

```text
M3 Oyuncu Profili
   ↓
M4 Formasyon
   ↓
M5 XI Adayları
   ↓
M6 Global Search
   ↓
M7 Gerçek Takım Ratingleri
   ↓
M7.2 PDF Taktik Mekanikleri
   ↓
M8 Chance Engine
   ↓
M9 Goal / Event Engine
   ↓
Poisson + Event-based Monte Carlo
   ↓
W / D / L
   ↓
M10 Formation Competition
   ↓
M6-B Exploration + Refinement
   ↓
DB2
   ↓
M11 Final Selector
   ↓
WEB
```

## MOTOR DURUMLARI

| Motor | Durum | Açıklama |
|---|---|---|
| M3 | ✅ | Skill, position/order/side, experience, form/loyalty, Specialty taşınıyor |
| M4 | ✅ | Legal formasyon havuzu korunuyor |
| M5 | ✅ | Formasyon başına geniş XI adayı üretiliyor |
| M6-A | ✅ | Formation-aware search + DB1 diversity |
| M7 | ✅ | L/C/R defence + midfield + L/C/R attack |
| M7.2 | ✅ | PDF taktik mekanikleri kodlandı; CI regression geçti |
| M8 | ✅ | PDF Eq1–Eq4 + tactic context + opportunity volumes; CI regression geçti |
| M9 | 🔧 | Event → goal genişletildi; PNF/PDIM + Appendix C C.1/C.2 utilities eklendi; hidden inputs hâlâ tamamlanacak |
| Monte Carlo | 🔧 | 18 adet 5-dakikalık event tick sampling aktif; full historical validation bekliyor |
| M10 | ✅/🔧 | Formation leaderboard hazır; MC'nin ranking'e tam bağlanması sıradaki adım |
| M6-B | ✅/🔧 | DB1 seed + refinement hazır; rank-driven depth geliştirilecek |
| DB2 | ✅ | Formation depth korunuyor |
| M11 | ✅/🔧 | Final selector hazır; gerçek event/MC çıktısıyla son kalibrasyon yapılacak |

## PDF MATCH ENGINE FAZLARI

```text
FAZ A  CHPP Specialty → Player                         ✅
FAZ B  M3 Specialty-aware profile                     ✅
FAZ C1 M8 discrete chance allocation                  ✅
FAZ C2 M9 chance-volume migration                     ✅
FAZ D  Historical chance-volume validation             ✅
FAZ E  PDF sector baseline + 60-match validation       ✅
FAZ F  AiM / AoW migration + M7.2 handoff             ✅
FAZ G  Pressing suppression                            ✅
FAZ H  Counter Attack opportunity engine               ✅
FAZ I  Long Shots opportunity engine                   ✅
FAZ J  Play Creatively event-volume layer              ✅
FAZ K  Specialty event engine                          🔧 active
FAZ L  Specialty ↔ tactic / weather                    🔜
FAZ M  M9 event-based goal resolution                  🔧 active
FAZ N  Historical event calibration                     🔜
FAZ O  Full event-based Monte Carlo                    🔧 active
FAZ P  Offline regression + real-match validation       🔜
```

## CI CHECKPOINT

03.09.2026 tarihinde M7.2/M8 compile regression kırmızıydı. Sorun, `AdvancedTacticalScenarioEngine` içindeki PDF alias sabitlerinin scope problemiydi. `PdfTacticalAliases` + global import ile düzeltildi.

Long Shots Faz I regression gate eklendi ve son temiz CI checkpoint'i:

```text
Commit: a66d03053f9e56f631b5be4e06521dc672afc69f
Workflow: HattrickAI V5 Deploy #423
M7 → M7.1 → M7.2 → M8 offline regression: PASS
FAZ I Long Shots opportunity regression: PASS
Build Docker image: PASS
Deploy on Azure VM: PASS
```

M9 geliştirmeleri sonrasında CI tekrar regression aşamasında çalışıyor. Bu nedenle yeni M9 adımları README'de tamamlanmış (`✅`) ilan edilmiyor; kod tamamlandıkça ve CI geçtiğinde checkpoint yükseltilecek.

FAZ I kapsamında production'da LS fırsat hacmi açıkça `LMR × tactic conversion` olarak korunuyor; paper aralığı `%6–43`. Long-shot'ın gole dönüşüm ilişkisi için Appendix C.2'nin yayınlanan doğrusal TCR fonksiyonu M9'a utility olarak eklendi; V5 tactic level → paper `RT` eşlemesi kanıtlanmadığı için production'a körlemesine uygulanmıyor.

## M3 — OYUNCU PROFİLİ

Specialty düz rating bonusu değildir. `Technical`, `Quick`, `Powerful`, `Unpredictable`, `Head` event bağlamında kullanılmak üzere profile taşınır.

## M4 / M5 / M6 — FORMASYON ARAMA

```text
M4 legal formations
        ↓
M5 candidates per formation
        ↓
M6-A formation-aware search
        ↓
DB1 minimum formation depth
        ↓
M10 formation leaderboard
        ↓
M6-B exploration/refinement
        ↓
DB2
        ↓
M11 final comparison
```

Hiçbir legal formasyon M6 öncesinde global ranking ile silinmez. DB1/DB2 formation diversity korunur.

## M7 — GERÇEK RATING

M3 oyuncu profilleri gerçek sektör ratinglerine dönüştürülür:

```text
Left Defence / Centre Defence / Right Defence
Midfield
Left Attack / Centre Attack / Right Attack
```

M8 ve M9 yeni rating uydurmaz; M7 ratinglerini kullanır.

## M7.2 — PDF TAKTİK KATMANI

Canonical tactic enum:

```text
Normal
Attack in the Middle (AiM)
Attack on the Wings (AoW)
Counterattack (CA)
Long Shots (LS)
Pressing (PR)
Play Creatively (PC)
```

M7.2 M7 ratinglerinden tactic context üretir ve M8'e canonical `M8TacticalMatchupInput` ile aktarır.

Makalenin yayınladığı mechanism/range değerleri production baseline olarak kullanılır:

```text
AiM  : wing → centre 20–35%
AoW  : centre → wing 34–52%
CA   : missed normal → counterattack 4–45%
LS   : LMR → long shot 6–43%
PR   : normal attack suppression 5–41% (one-team PR case)
PC   : own special-event multiplier 2.37×–3.80×
```

Mevcut V5 tactic level `0–10` ölçeğinin paper'daki `RT` ile birebir eşdeğer olduğu kanıtlanmadığı için Appendix C regresyonları doğrudan production coefficient kabul edilmez.

## M8 — CHANCE ENGINE

M8 canonical flow:

```text
Eq.1  midfield → possession
Eq.2  5 exclusive + 5 shared structure
Eq.3  L / C / R / DFK / IFK / PK distribution
Eq.4  attack vs defence sector scoring probability
        ↓
AiM / AoW migration
CA opportunity
Pressing suppression
LS opportunity
```

PDF Eq.3 baseline:

```text
Left   25.65%
Centre 36.15%
Right  25.65%
DFK     5.86%
IFK     4.18%
PK      2.51%
```

LMR toplamı `%87.45`; expected normal attack volume `10`; dolayısıyla expected LMR volume `8.745`.

60 CHPP maçlık validation datasetinde gözlenen LMR ortalaması `8.80` bulunmuştur. Bu veri production coefficient'lerini otomatik değiştirmemiştir.

M8 artık tactic-specific opportunity hacimlerini de açıkça üretir:

```text
Pressing   → suppressed Normal volume
CA         → opponent missed Normal → CA opportunity
LS         → LMR → Long Shot opportunity
AiM / AoW  → sector migration
PC         → special-event volume multiplier context
```

## M9 — EVENT → GOAL ENGINE

PDF Tables 4–5 üzerinden event sınıfları kodlanmıştır:

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

Player-based baseline `Binomial(n=4,p=0.841)` ve team-based baseline `Binomial(n=5,p=0.372)` korunur. Event eligibility current XI'daki Specialty + position üzerinden belirlenir.

### Appendix C mekanizmaları

M9'a aşağıdaki yayınlanmış ilişkiler doğrudan utility olarak işlendi:

```text
C.1 Set-piece goal probability
ATTACKSP = -0.0000380429·d³
           + 0.0000226846·d²
           + 0.0366246·d
           + 0.45515

where d = ISP attack − opponent ISP defence

C.2 Long Shot tactic conversion
TCR_LS(RT) = 0.00761935·RT + 0.07520052

C.3 Powerful Normal Forward
PNF=1: CD 0/1/2/3 → 9.6% / 6.9% / 3.3% / 2.0%
PNF=2: CD 0/1/2/3 → 11.7% / 9.6% / 5.2% / 3.1%
PNF≥3 → 6.6%

PDIM:
1 PDIM → average 6.5% normal-attack suppression
```

PNF artık başarısız normal denemeler üzerinden extra-attack hacmi üretir; PDIM normal attack volume'ünü azaltan ayrı bir suppression signal üretir. Opponent CD/specialty detayları CHPP inputuna bağlanana kadar default CD=3 yalnızca geriye dönük API uyumluluğu için kullanılır.

Henüz production'a otomatik uygulanmayan girdiler:

```text
Set-piece taker skill
Long Shot scoring probability / graph calibration
Opponent Specialty detail
Specialty ↔ weather / tactic cross-effects
V5 tactic-level → paper RT exact mapping
```

## EVENT-BASED MONTE CARLO

Makale gerçek motorun dinamik olduğunu ve olayların 90 dakika boyunca yaklaşık 5 dakikalık aralıklarla oluştuğunu; çalışmanın ise ortalama statik temsil kullandığını belirtiyor.

V5 M9 simulation katmanı artık **18 × 5 dakika tick** üzerinden örnekleme yapıyor:

```text
M8 chance / xG budget
      ↓
18 match ticks
      ↓
Normal chance goal/no-goal sampling
      ↓
M9 event eligibility
      ↓
Event occurrence sampling
      ↓
Event goal conversion
      ↓
90-minute score
      ↓
1000-match score distribution
      ↓
W / D / L
```

Simulation deterministic seed kullanır; offline regression aynı seed ile aynı most-likely score ve W/D/L üretimini kontrol eder.

Şu an eksik olan bölüm: opponent tarafının tüm specialty eventleri ve Long Shot scoring graph'ının tarihsel calibration ile production'a bağlanması. Bunlar tamamlanmadan Monte Carlo `tam Hattrick motoru` olarak ilan edilmeyecek.

## CALIBRATION KURALI

```text
PDF / verified mechanism
          ↓
production baseline
          +
CHPP historical observations
          ↓
error / residual analysis
          ↓
confidence test
          ↓
only if justified → production adjustment
```

Amaç `tek maça göre motoru eğmek` değil, large-sample calibration yapmaktır.

## UYGULAMA SIRASI

```text
1. CI/build temizliği                                  ✅
2. M7.2 → M8 tactic handoff stabilizasyonu            ✅
3. M8 chance-volume + tactic effects stabilization     ✅
4. M9 event → goal genişletme                          🔧 active
5. LS + PNF/PDIM                                     🔧 PNF/PDIM + C.1/C.2 utility done
6. Specialty ↔ weather/tactic                         🔜
7. Historical event dataset                            🔜
8. Event-based Monte Carlo                             🔧 18-tick sampling done
9. M10 ← MC sonuçları                                  🔜 NEXT
10. M6-B rank/depth refinement                         🔜
11. M11 risk-adjusted final                            🔜
12. Offline + gerçek maç regression                    🔜
```

## KAYNAK REFERANSI

Ana paper:

`Anthony C. Constantinou, Nicholas Higgins, Neville K. Kitson — Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning, Entertainment Computing 57 (2026) 101131.`

DOI: `10.1016/j.entcom.2026.101131`
