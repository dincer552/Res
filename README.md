# HattrickAI V5

## Aktif geliştirme hattı

**Aktif branch: `v5`**

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı, aday üreten ve gerektiğinde geri beslemeli motor mimarisiyle geliştirir.

## Nihai hedef akış — V5.1 mimari planı

```text
M1  Veri / CHPP
 │
 ├──────────────→ M2  Rakip Analizi
 │
 └──────────────→ M3  Oyuncu Analizi
                         │
                         ↓
                  M4  Diziliş Adayları
                         │
                         ↓
                  M5  XI Adayları
                         │
                         ↓
                  M6  Individual Behaviour Adayları
                         │
                         ↓
                  M6B Maç Ayarı Adayları
                  ├─ Team Attitude
                  ├─ Team Tactic
                  └─ diğer maç parametreleri
                         │
                         ↓
                  M7  Rating Simulation
                         │
                         ↓
                  M8  Matchup Engine
                         │
                         ↓
                  M9  Tactical Value
                         │
                         ↓
                  M10 Global Match Optimizer
                    ↙       ↓        ↘
                  M4        M5       M6/M6B
                    \        │        /
                     └──→ M7 → M8 → M9
                              │
                         stabil aday
                              ↓
                       M11 Prediction
```

### Mimari prensip

Her motor kendisinden önceki katmanın bilgisini kullanır; kendisinden sonraki motorun kararını erkenden vermemelidir. Aday üreten motorlar tek bir erken karar vermek yerine yeterli alternatifleri korur. M10, tüm aday uzayını karşılaştıran kontrollü global optimizerdır.

---

## Motor isimleri ve hedef durum

| Sıra | Motor | Gerçek işlev | Durum |
|---|---|---|---|
| M1 | CHPP Veri Motoru | Veri toplama/normalizasyon | ✅ |
| M2 | Rakip Analiz Motoru | Rakip profili ve tehdit verisi | 🟡 Bakım + doğrulama |
| M3 | Oyuncu Analiz Motoru | Oyuncu/pozisyon profilleri | ✅ Offline PASS |
| M4 | Aday Diziliş Motoru | Yasal/doldurulabilir formation adayları | ✅ Offline PASS |
| M5 | Pozisyon Optimizasyon Motoru | Formation başına XI adayları | 🟢 Düzeltildi / regression PASS hedefi |
| M6 | Davranış Aday Motoru | Individual behaviour adayları | 🟡 İskelet |
| M6B | Maç Ayarı Aday Motoru | Team attitude + team tactic adayları | 🆕 Planlandı |
| M7 | Bölgesel Rating Motoru | Senaryo → 7 bölgesel rating | 🟢 M7/M7.1 implementation + regression PASS |
| M8 | Maç Eşleşme Motoru | Biz ↔ rakip bölgesel matchup | ⏳ |
| M9 | Taktiksel Değer Motoru | Fırsat + güvenlik + midfield + risk | ⏳ |
| M10 | Global Match Optimizer | Formation × XI × behaviour × settings optimizasyonu | ⏳ |
| M11 | Maç Tahmin Motoru | Final plan → olasılık/tahmin | ⏳ |

---

## Motor sorumlulukları

### M1 — CHPP Veri Motoru / Veri Katmanı

Ham takım, oyuncu, rakip, maç, training ve ilgili CHPP verilerini sağlar ve normalize eder. Karar vermemelidir.

### M2 — Rakip Analiz Motoru

Rakibin bizim XI seçimimizden bağımsız profilini üretir:

- son resmi maç
- mümkünse recent baseline (son 3–5 maç)
- formasyon
- final 11
- gerçek 7 bölgesel rating
- tehdit profili
- veri kaynağı
- confidence

**RP karar girdisi değildir.** M2 bizim XI'ımızı seçmez ve değiştirmez.

### M3 — Oyuncu Analiz Motoru

Sadece kendi oyuncularının doğal pozisyon uygunluk profilini üretir. XI, formation, rakip matchup veya takım ratingi seçmez. Profilde `IsEligible`, `InjuryLevel`, pozisyon adayları, primary ve secondary position bulunur. `InjuryLevel == 999` oyuncular aday değildir.

### M4 — Aday Diziliş Motoru

Yasal ve doldurulabilir formation adaylarını üretir. Mevcut adaylar:

- 3-5-2
- 3-4-3
- 4-4-2
- 4-5-1
- 2-5-3
- 5-3-2

M4 formation adaylarını sıralayabilir ancak rakibe karşı nihai seçim yapmaz. Structural Score aday üretme sinyalidir; M5/M8/M9 sonuçlarıyla karıştırılmamalıdır.

### M5 — Pozisyon Optimizasyon Motoru

Her `FormationCandidate` için oyuncu → slot eşleşmesini optimize eder. 11 slotun tamamı dolmalı, aynı oyuncu aynı XI içinde iki kez kullanılamamalı ve eligibility tekrar kontrol edilmelidir.

Ana optimizasyon tüm uygun oyuncu havuzunda exact Hungarian assignment ile yapılır. Alternatif XI'lar geniş aday havuzu/beam search ile korunur.

M5'te:

- M3 suitability temel sinyaldir.
- Natural Role yalnızca küçük tie-breaker olmalıdır.
- Role Distance yakın adaylar arasında tercih sinyalidir; M3 skorunu ezmemelidir.
- Continuity verisi güvenilir biçimde mevcutsa küçük bonus olarak kullanılabilir.
- Balance sinyali M5'te gerçek rating/crowding hesabının yerine geçmemelidir.

M5 rakibe, team tactic'e veya individual behaviour'a göre gizli karar vermez.

### M6 — Individual Behaviour Candidate Engine

M5'ten gelen her XI için mümkün individual behaviour adaylarını üretir:

- Normal
- Offensive
- Defensive
- Towards Middle
- Towards Wing
- pozisyona göre yasal diğer seçenekler

M6 nihai davranış kararını vermez; adayları M7/M8/M9 değerlendirmesine bırakır.

**Database/learning:** M6 aşamasında database tabanlı davranış öğrenmesi uygulanmayacaktır. Yeterli gerçek maç verisi ve eşleşmiş pre-match snapshot birikmeden geçmiş sonuçları M6'nın deterministik aday üretimine bağlamak güvenilir değildir. Gelecekte historical evidence gerekirse M9/M10 tarafında kontrollü olarak değerlendirilecektir.

### M6B — Match Configuration Candidate Engine

M6'dan ayrı tutulur. Individual order ile takım ayarlarının birbirine karışmasını önler. Aday uzayına:

- Team Attitude (Normal/PIC/MOTS vb. uygun seçenekler)
- Team Tactic (Normal, Counter Attack, Attack in Middle, Attack on Wings, Pressing, Long Shots, Play Creatively vb. uygun seçenekler)
- ileride eklenebilecek maç ayarları

eklenir.

M6B nihai seçimi yapmaz. Yasal/uygulanabilir kombinasyonlar üretir.

### M7 — Bölgesel Rating / Scenario Simulation Engine

M7 yalnızca XI değil, bir **Match State** senaryosunu değerlendirmelidir:

```text
XI
+ Individual Behaviour Set
+ Team Attitude
+ Team Tactic
+ Team Spirit
+ diğer doğrulanmış maç parametreleri
→ 7 bölgesel rating
```

Çıktı `RatingCandidateId`, `FormationId`, `LineupId`, `BehaviourSetId`, `TacticId` gibi izlenebilir kimlikleri taşımalıdır. Böylece M8'de bir ratingin hangi adaydan geldiği kaybolmaz.

M7'nin temel rating katsayıları, yeni doğrulanmış gerçek maç verisi olmadan değiştirilmemelidir.

### M7.1 — Team Spirit Context Layer

Team Spirit M7 rating senaryosunun ayrı bir context girdisidir. Team Spirit etkisi yalnızca **midfield** üzerinde uygulanmalı; diğer altı bölgesel ratingi değiştirmemelidir.

Mevcut uygulama/regression yaklaşımında Team Spirit eğrisi:

```text
TeamSpiritMultiplier = 0.10 + 0.425 × sqrt(TeamSpirit)
```

olarak korunur.

Bu katman M7'nin deterministic yapısını bozmaz ve M8'e gönderilecek rating candidate içinde Team Spirit context'ini izlenebilir tutar.

### M7.2 — Advanced Tactics Layer / sonraki aşama

M7.2'nin amacı M7'nin temel 7 bölgesel rating çekirdeğini gereksiz yere yeniden yazmak değil, doğrulanmış takım taktik etkilerini ayrı ve izlenebilir bir katman olarak eklemektir.

Kapsam:

- Normal / PIC / MOTS bağlamı
- Team Tactic ve tactic level
- Attack in the Middle (AIM)
- Attack on Wings (AOW)
- Counter Attack
- Pressing
- Long Shots
- Play Creatively
- diğer doğrulanmış taktikler

Taktik etkileri tek bir kaba `rating × sabit katsayı` formülüne indirgenmeyecek. **Rating effect** ile **chance generation / chance distribution effect** ayrı tutulacaktır. M7 rating üretir; M8/M9 tarafı matchup ve maç değerini değerlendirecektir.

M7.2 ilk aşamada katsayı kalibrasyonu yapmayacak; önce tüm taktik/interaction senaryolarını deterministik ve izlenebilir biçimde üretilebilir hale getirecektir. Gerçek CHPP maçları geldikçe kalibrasyon yapılacaktır.

### M8 — Matchup Engine

M8, bizim M7 rating adaylarımızı M2'nin rakip profilindeki gerçek/reference ratinglerle karşılaştırır.

Bölgesel eşleşmeler:

```text
Our ATT-L ↔ Opp DEF-L
Our ATT-C ↔ Opp DEF-C
Our ATT-R ↔ Opp DEF-R

Opp ATT-L ↔ Our DEF-L
Opp ATT-C ↔ Our DEF-C
Opp ATT-R ↔ Our DEF-R

Our MID ↔ Opp MID
```

Her bölge için mümkün olduğunca:

- Our Rating
- Opponent Rating
- Absolute Difference
- Ratio/relative strength
- Attack Opportunity
- Defensive Security
- Risk
- Confidence
- Data Source

tutulmalıdır.

M8 oyuncu, formation veya behaviour seçmez. Gol/kazanma olasılığı da üretmez.

### M9 — Tactical Value Engine

M8 matchup sonuçlarını maç açısından değerlendirir:

- Attack Opportunity
- Defensive Security
- Midfield Control
- Risk / balance
- veri güveni

M9'un görevi "en iyi oyuncuyu" seçmek değil, aday maç planının rakibe karşı değerini çıkarmaktır.

### M10 — Global Match Optimizer

M10 gerçek global optimizasyon katmanıdır. Aday uzayı:

```text
Formation
× XI
× Individual Behaviour Set
× Team Attitude
× Team Tactic
```

üzerinde M7 → M8 → M9 sonuçlarını karşılaştırır.

M10 gerektiğinde kontrollü geri besleme verir:

- daha iyi formation aranıyorsa → M4
- daha iyi XI gerekiyorsa → M5
- daha iyi behaviour gerekiyorsa → M6
- daha iyi team setting gerekiyorsa → M6B

Sonra aday tekrar M7 → M8 → M9 zincirinden geçer.

Döngü sonsuz olmamalıdır. Maksimum iteration sayısı ve/veya anlamlı skor iyileşmesi (`epsilon`) ile durdurulmalıdır. Aynı aday tekrar tekrar değerlendirilmemeli; `CandidateId`/hash ile deduplication yapılmalıdır.

M10 ayrıca **Immediate Match Value** ile **Long-Term Cost** bilgisini ayrı tutmalıdır. Özellikle PIC/MOTS gibi kararlar yalnızca anlık rating artışıyla değerlendirilmemelidir.

### M11 — Match Prediction Engine

M10'un stabilize edilmiş final maç planını alır ve:

- pozisyon/atak fırsatı
- gol olasılığı
- beraberlik/kazanma/kaybetme olasılığı
- mümkünse confidence interval

üretir.

M11 formation, XI veya behaviour seçmez.

---

# Veri sözleşmeleri — kritik zincir

M8 ve sonrası için adayın izlenebilirliği korunmalıdır:

```text
M5 PositionCandidate
    ↓
M6 BehaviourCandidate
    ↓
M6B MatchConfigurationCandidate
    ↓
M7 RatingCandidate
    ↓
M8 MatchupCandidate
    ↓
M9 TacticalCandidate
    ↓
M10 FinalMatchPlan
    ↓
M11 Prediction
```

M7 ratingi gerçek CHPP ratingiyle karıştırılmamalıdır. Rakibin CHPP'den gelen ratingi `GroundTruth/Reference`, bizim M7 ratingimiz `Prediction/Scenario` olarak işaretlenmelidir.

---

# Geri besleme döngüsü

Eski tasarımda M9 → M5 doğrudan dönüş vardı. Nihai tasarımda bu dönüş **M10 Global Optimizer tarafından kontrollü şekilde yönetilir**.

```text
M4 → M5 → M6 → M6B → M7 → M8 → M9
                         ↓
                    M10 Optimizer
                  ↙      ↓       ↘
                M4       M5       M6/M6B
                  \       │       /
                   └→ M7 → M8 → M9
```

Amaç yalnızca toplam ratingi büyütmek değildir. Amaç:

**oyuncu + pozisyon + davranış + takım ayarı + rakip matchup**

bütününü optimize etmektir.

---

# V5.1 yol haritası

## Faz 1 — M1–M5 stabilizasyonu

- M1 veri sözleşmesini sabitle
- M2 opponent profile + source/confidence
- M3 player profile regression
- M4 six-formation regression
- M5 exact assignment + geniş alternatif arama
- eski `XIOptimizer` yolunun canlı zincirdeki kullanımını netleştir

## Faz 2 — M6 / M6B

- M5'ten tüm kaliteli XI adaylarını al
- legal individual behaviour kombinasyonlarını üret
- team attitude ve team tactic adaylarını ayrı katmanda üret
- adayları erken eleme yerine M7'ye taşı
- database/learning katmanını bu aşamada uygulama

## Faz 3 — M7 / M7.1

- XI + behaviour + team setting → rating senaryosu
- CandidateId zinciri
- 7 bölgesel rating
- Team Spirit → midfield context
- baseline regression
- historical calibration için temiz veri toplama

## Faz 4 — M7.2 Advanced Tactics

- tactic + tactic level context
- rating effect ile chance effect ayrımı
- AIM/AOW/CA/Pressing/LS/PC senaryoları
- individual order × tactic × attitude interaction testleri
- deterministik regression

## Faz 5 — M8 Matchup

- 7 bölgesel matchup
- attack opportunity
- defensive security
- midfield control
- risk
- confidence
- tüm M7 adaylarını rakibe karşı değerlendirme

## Faz 6 — M9 Tactical Value

- M8 çıktılarından maç değeri
- risk/fırsat dengesi
- tactic-dependent trade-off'lar

## Faz 7 — M10 Global Optimization

- Formation × XI × Behaviour × Settings kombinasyonları
- deduplication
- beam/pruning
- kontrollü geri dönüş
- convergence/epsilon
- immediate value vs long-term cost

## Faz 8 — M11 Prediction

- final stabilized plan
- goal probability
- W/D/L probability
- confidence

---

# M7 historical validation / regression kararı — 2026-09-01

Mevcut offline CHPP snapshot içindeki geçmiş maçlar ayrıca incelendi.

### Kullanılabilir geçmiş veri

S4MSUNFC ve Zeytinburnu Sahil Spor için geçmiş maç kayıtları mevcut. Ancak geçmiş maçların büyük bölümü yalnızca maç listesi/sonuç verisi içeriyor. Gerçek 7 bölgesel rating içeren ayrıntılı `matchdetails.xml` kaydı bu snapshot'ta sınırlı.

Kullanılabilir ayrıntılı örnek:

**30 Ağustos 2026 — bombacı mülayim spor 3–2 Zeytinburnu Sahil Spor**

Bu maçta gerçek CHPP 7 bölgesel ratingleri mevcut. Ancak geçmiş maçın o günkü oyuncu skill/form snapshotı ile mevcut oyuncu snapshotı birebir aynı olmadığı için bu kayıt **temiz historical replay calibration dataseti** olarak kabul edilmemelidir.

### Karar

Bu tek geçmiş maçtan M7/M7.1 temel katsayıları değiştirilmemelidir.

M7/M7.1 durumu:

- **Implementation/Regression:** PASS
- **Historical calibration:** BEKLEMEDE
- **M7.2:** Sıradaki geliştirme

### Gelecekte gerçek maç verisiyle yapılacak test

Kullanıcı yeni maçlar oynadıkça mümkünse maç öncesi CHPP snapshot + maç sonrası `matchdetails.xml` birlikte saklanacaktır. Yeterli örnek biriktiğinde:

1. maç öncesi oyuncu snapshotı
2. formation
3. individual orders
4. team attitude
5. team spirit
6. tactic/tactic level
7. home/away
8. gerçek 7 bölgesel rating

eşleştirilerek M7/M7.1 ile karşılaştırılacaktır.

Ölçülecek metrikler:

- bölge bazlı absolute error
- MAE
- ortalama sapma/bias
- yüzde hata
- systematic under/over-estimation
- Team Spirit etkisinin gerçek maçlarla doğrulanması

Yeterli gerçek veri olmadan katsayı değiştirilmez.

---

# Test stratejisi

Her motor için sıra:

1. Kod kontrolü
2. Girdi/çıktı sözleşmesi kontrolü
3. Önceki motorla entegrasyon testi
4. Offline CHPP regression
5. Sonuç analizi
6. Gerekli düzeltme
7. Commit
8. Build/deploy
9. Deploy doğrulaması
10. Sonraki motorun nihai karar mantığına geçiş

### Kalıcı offline senaryo

`TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json`

Şema:

`hattrickai-v5-offline-test-v2`

Ana senaryo:

- **Biz:** S4MSUNFC (`1080139`)
- **Rakip:** Zeytinburnu Sahil Spor (`653953`)
- **Maç:** `769648177`
- **Tarih:** 2026-09-06 15:00 UTC
- **Saha:** Zeytinburnu Sahil Spor
- **Durum:** S4MSUNFC deplasman

### Ground-truth rakip ratingi

```text
DEF-L 6.25
DEF-C 9.50
DEF-R 6.25
MID   7.00
ATT-L 10.00
ATT-C 13.00
ATT-R 8.50
```

### Mevcut S4MSUNFC referans ratingi

```text
Diziliş: 3-5-2
DEF-L 9.50
DEF-C 16.00
DEF-R 9.75
MID   7.50
ATT-L 9.50
ATT-C 11.50
ATT-R 10.00
```

Bu iki rating seti birbirine karıştırılmamalıdır: CHPP'den gelen gerçek rating ground-truth/reference, motorun ürettiği rating prediction/scenario'dur.

---

# Son doğrulanmış test özeti

### M3 — PASS ✅

Pozisyon profilleri üretildi. `InjuryLevel == 999` oyuncular aday dışı. M3 yalnızca oyuncu profili üretmektedir.

### M4 — PASS ✅

6 yasal diziliş 11 farklı uygun oyuncuyla doldurulabildi. Structural Score sıralaması:

| Sıra | Diziliş | Structural Score |
|---|---|---:|
| 1 | **3-5-2** | **17.977** |
| 2 | **3-4-3** | **17.907** |
| 3 | **2-5-3** | **17.701** |
| 4 | **4-5-1** | **17.556** |
| 5 | **4-4-2** | **17.365** |
| 6 | **5-3-2** | **16.035** |

3-5-2 ile 3-4-3 arasındaki fark yalnızca **0.070** olduğundan M4'ün diğer adayları erken elememesi gerekir.

### M5 — PASS / geliştirilmiş 🟢

Yeni M5 tüm uygun oyuncu havuzunda exact Hungarian assignment kullanır; alternatif aday araması da genişletilmiştir. Natural Role artık M3 suitability'yi ezmeyen küçük bir tie-breaker olarak kullanılmaktadır.

Aynı offline senaryoda en iyi M5 toplamları:

| Diziliş | M5 toplam suitability |
|---|---:|
| **3-5-2** | **197.89** |
| **3-4-3** | **197.28** |
| **2-5-3** | **195.08** |
| **4-5-1** | **193.44** |
| **4-4-2** | **191.23** |
| **5-3-2** | **177.15** |

Sıralama M4 ile tutarlı kalmıştır. M5 rakip/taktik davranış kararını üstlenmemektedir.

### M6 — PASS / candidate generation 🟢

Legal individual behaviour candidate matrisi üretildi. Örnek 3-5-2 senaryosunda davranış uzayı yaklaşık **248.832** kombinasyona ulaşabiliyor. M6 bu uzayın tamamını gereksiz RAM objelerine dönüştürmek yerine candidate matrix + kontrollü enumeration yaklaşımını kullanır.

M6 için database/learning katmanı bu aşamada uygulanmamıştır.

### M7 — PASS / implementation + regression 🟢

M7 7 bölgesel rating senaryosu üretiyor. Individual orders, overcrowding, venue, attitude, tactic context, form ve experience gibi mevcut doğrulanmış hesaplar korunmuştur.

### M7.1 — PASS / Team Spirit 🟢

Team Spirit etkisi yalnızca midfield'e uygulanıyor. Test snapshotında `TeamSpirit = 3` için Team Spirit multiplier yaklaşık **0.8361** ve mevcut baseline midfield değeri yaklaşık **6.25 → 5.25** seviyesine geliyor; diğer 6 bölge değişmiyor.

Bu test implementation/regression PASS'tir; henüz gerçek historical calibration değildir.

---

# Tasarımda kesin sınırlar

- M3 rakip bilmez.
- M4 rakip sonucu seçmez.
- M5 rakip/taktik davranış seçmez.
- M6 final behaviour seçmez; aday üretir.
- M6B final team setting seçmez; aday üretir.
- M7 rating üretir ama maç kazanma olasılığı üretmez.
- M7.1 Team Spirit'i rating context olarak işler; yalnızca midfield etkisi uygular.
- M7.2 rating etkisi ile chance effect'i ayrı tutar.
- M8 oyuncu/diziliş seçmez; matchup üretir.
- M9 final plan seçmez; tactical value üretir.
- M10 global kararı verir ve gerektiğinde önceki aday motorlarına kontrollü geri döner.
- M11 final prediction üretir; planı değiştirmez.

Database/learning katmanı şimdilik uygulanmıyor. Gerçek maç sonuçları ve eşleşmiş pre-match snapshotlar yeterli hacme ulaştığında M9/M10 tarafında historical evidence olarak ayrıca değerlendirilecek; M6/M7'nin deterministik temel matematiğine erken dönemde bağlanmayacak.

Bu README, V5'in mevcut teknik durumunu, doğrulanmış regression sonuçlarını, historical validation kararını ve M7.2 sonrası hedef mimariyi ana teknik kayıt olarak tutar.
