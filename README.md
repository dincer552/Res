# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-02

Aktif branch: `v5`

V5 artık yalnızca M3-M6 prototipi değil; **canlı analiz isteği üzerinde M3 → M10 motor zinciri bağlanmış durumdadır.** Mevcut hedef, zincirin gerçek web analizinde güvenilir şekilde çalışmasını sağlamak ve ardından kalibrasyon / optimizasyon derinliğini artırmaktır.

### Güncel motor durumu

```text
M3   Oyuncu Analizi                    ✅ LOCK / LIVE
 ↓
M4   Formasyon adayları                ✅ LIVE
 ↓
M5   XI / pozisyon adayları            ✅ LIVE
 ↓
M6   Global XI + behaviour             🟡 ACTIVE / LIVE
 ↓
M7   Regional Rating Scenario          ✅ INTEGRATED
 ↓
M7.2 Advanced Tactical Scenario        ✅ INTEGRATED
 ↓
M8   Chance / Matchup                  ✅ INTEGRATED
 ↓
M9   Match Prediction                  ✅ INTEGRATED
 ↓
M10  Final Decision                    ✅ INTEGRATED
 ↓
WEB  Final M10 XI + Individual Order   ✅ CONNECTED
```

### En önemli mevcut durum

- `AnalysisService` artık eski doğrudan XI yerleştirme yolunu kullanmıyor.
- `/api/v5/analysis` çağrısı M3 → M4 → M5 → M6 → M7 → M7.2 → M8 → M9 → M10 zincirinden geçiyor.
- Web arayüzünde gösterilen **önerilen kendi 11'i M10'un `FinalPlan` çıktısından geliyor.**
- M10 seçiminin ardından final lineup aynı zamanda saha üzerinde çiziliyor.
- Her oyuncunun saha kutusunda **RP değeri + Individual Order** gösteriliyor.
- Individual Order seçenekleri motor tarafından seçilen final lineup üzerinde korunuyor.
- Eski sabit/doğrudan XI placement yolu final önerinin kaynağı olmaktan çıkarıldı.

## Motorların mevcut görevleri

### M3 — Player Analysis

Oyuncu havuzunu analiz eder ve oyuncu → pozisyon suitability profillerini üretir.

Kontrol edilen temel alanlar:

- eligibility
- pozisyon suitability
- oyuncu becerileri
- form
- loyalty
- experience
- stamina
- Individual Order etkisine temel oluşturacak oyuncu profili

**Durum:** LOCK. M3 temel katsayıları yeni gerçek maç verisiyle doğrulanmadan gereksiz şekilde değiştirilmemelidir.

### M4 — Formation Candidate Engine

M3 çıktısından doldurulabilir ve legal formasyon adayları üretir.

Amaç:

- legal formation üretmek
- 11 slotluk yapıyı korumak
- structural score üretmek
- M5'e deterministik formation handoff yapmak

**Durum:** LIVE / validated.

### M5 — Position / XI Candidate Generator

Her legal formasyon için oyuncu-slot eşleşmelerinden XI adayları üretir.

Korunan kurallar:

- 11/11 slot
- aynı oyuncunun XI içinde iki kez kullanılmaması
- eligibility
- M3 suitability continuity
- formation / slot uyumu
- CandidateId / FormationId / LineupId izlenebilirliği
- alternatif adayların erken elenmemesi
- exact assignment yaklaşımı

Canlı web isteğinde request-time maliyetini sınırlamak için **formasyon başına en güçlü 6 XI adayı** M6'ya aktarılır.

**Durum:** LIVE / validated.

### M6 — Global Optimization

M6, M5'ten gelen XI adaylarını daha sonraki rating / tactical / matchup değerlendirmesiyle birlikte optimize eder.

Mevcut canlı zincir:

```text
M5 XI
 ↓
M6 behaviour / global search
 ↓
M7 Regional Rating
 ↓
M7.2 Advanced Tactical
 ↓
M8 Chance / Matchup
 ↓
Tactical Candidate Score
```

Mevcut canlı arama sınırları:

- beam width: `6`
- maksimum iteration: `4`
- M5: formasyon başına maksimum `6` XI

Amaç, devasa Cartesian behaviour uzayını körlemesine RAM'e yüklemek yerine kontrollü arama yapmaktır.

**Durum:** ACTIVE. Matematiksel ve davranışsal optimizasyonun daha derin kalibrasyonu sonraki geliştirme alanıdır.

### M7 — Regional Rating Scenario

Tam lineup için bölgesel rating senaryosu hesaplar.

7 ana sektör:

- Left Defence
- Central Defence
- Right Defence
- Midfield
- Left Attack
- Central Attack
- Right Attack

M7 ayrıca match state üzerinden:

- home / away
- team attitude
- team tactic
- team spirit
- maç dakikası
- gol farkı

gibi bağlamları taşıyabilecek yapıdadır.

**Durum:** INTEGRATED. Mevcut canlı pipeline'da `TeamTactic.Normal` kullanılıyor; takım taktiğinin ayrıca optimize edilmesi sonraki aşamadır.

### M7.2 — Advanced Tactical Scenario

M7 rating çıktısı üzerinde gelişmiş taktik senaryosunu hesaplar.

Mevcut modelde:

- tactic skill
- tactical level
- chance distribution
- tactical input totals
- pressing / counter / long shots / creative gibi taktik yapıların veri modeli

bulunur.

**Durum:** INTEGRATED. Gerçek maç kalibrasyonu henüz tamamlanmış değildir.

### M8 — Chance / Matchup

M7 + M7.2 senaryosunu rakibin bölgesel ratingleriyle karşılaştırarak yapısal şans üretir.

Pipeline'da kullanılan temel çıktılar:

- midfield share
- left attack vs right defence
- central attack vs central defence
- right attack vs left defence
- structural chance index

Bunlardan ayrıca lineup'ın savunma tarafı için matchup marjları oluşturulur.

**Durum:** INTEGRATED.

### M9 — Match Prediction

M8 structural chance çıktısını sınırlı ve deterministik bir maç tahminine dönüştürür.

Çıktılar:

- expected goals
- win probability
- draw probability
- loss probability
- possession / midfield probability

**Önemli:** M9 şu an yapısal modeldir; yeterli gerçek maç verisiyle tarihsel calibration henüz tamamlanmamıştır.

**Durum:** INTEGRATED / calibration bekliyor.

### M10 — Final Decision

M10, daha önce değerlendirilmiş tactical candidate'lar arasından deterministik final karar katmanıdır.

Composite karar yapısında:

- tactical score
- prediction win probability
- structural chance

birlikte kullanılır.

M10 çıktısı:

- `BestPlan`
- final lineup
- final rating
- matchup
- tactical score
- prediction
- ranking

**Mevcut sınırlama:** Canlı pipeline şu anda M6'nın en iyi adayını M9'dan geçirip M10'a tek aday olarak veriyor. Dolayısıyla M10 bugün gerçek anlamda çoklu aday havuzunu yeniden sıralayan geniş bir global optimizer değil; **M6'nın en iyi sonucunu deterministik şekilde final plana çeviren son karar katmanıdır.** İleride M10'a birden fazla güçlü M6 adayı verilmesi planlanmaktadır.

**Durum:** INTEGRATED / ACTIVE.

---

# Web sitesi — mevcut durum

## Analiz butonu

Mevcut analiz butonu `/api/v5/analysis` endpoint'ini kullanır.

Akış:

```text
Kullanıcı → Analiz
       ↓
CHPP veri toplama
       ↓
M3
       ↓
M4
       ↓
M5
       ↓
M6
       ↓
M7
       ↓
M7.2
       ↓
M8
       ↓
M9
       ↓
M10
       ↓
FinalPlan
       ↓
Web saha
```

Böylece web arayüzünün önerdiği XI ile motor zincirinin ürettiği XI aynı final kaynaktan gelir.

## V5 Motor Logları

Siteye mevcut `Deploy logları` kutusunun üzerine yeni bir:

**🧠 V5 Motor Logları • M3 → M10**

kutusu eklenmiştir.

Bu kutu analiz sonrasında gerçek API cevabındaki `motorPipeline` verisini okuyarak şunları gösterir:

- **M3:** analiz edilen oyuncu sayısı
- **M4:** formation aday sayısı ve lider aday
- **M5:** XI aday sayısı ve suitability
- **M6:** iterations / evaluated / retained / convergence
- **M7:** bölgesel ratingler ve confidence
- **M7.2:** tactic / level / chance distribution
- **M8:** structural chance / midfield / L-C-R attack shares
- **M9:** xG / win / draw / loss
- **M10:** ranking / seçilen formasyon / final Individual Orders

Log kutusu collapsible'dır ve analiz cevabı geldikten sonra otomatik olarak güncellenir.

## Saha üzerindeki oyuncu kutuları

Final M10 lineup'ındaki oyuncuların saha kutularında artık:

```text
Oyuncu adı
RP = ...
OFANSİF / DEFANSİF / MERKEZE / KANA / Normal
```

bilgileri gösterilir.

Individual Order görsel olarak da ayırt edilir:

- Ofansif
- Defansif
- Merkeze
- Kana
- Normal

Bu bilgiler doğrudan final `Lineup.Slots[].Order` değerinden gelir.

## Frontend entegrasyonu

Yeni frontend katmanları:

- `motor-logs.js` → gerçek M3-M10 pipeline sonucunu webde gösterir.
- `motor-render.js` → final lineup üzerindeki Individual Order bilgisini saha kutularına taşır.
- Docker build sırasında bu scriptler `index.html` içine otomatik olarak eklenir.

---

# 2026-09-02 — Canlı analiz hatası ve questionnaire etkisi

## Tespit edilen canlı hata

Kullanıcı web arayüzünde `ANALİZİ ÇALIŞTIR` akışını başlatıp üç maç koşulunu girdikten sonra son adımda **`Failed to fetch`** hatası görülebilmektedir.

Mevcut frontend akışı mantıksal olarak doğrudur:

```text
ANALİZİ ÇALIŞTIR
      ↓
questionnaire POST
      ↓
/api/v5/analysis
      ↓
CHPP + M3 → M10
      ↓
FinalPlan
```

`Failed to fetch` normal bir HTTP 4xx/5xx cevabından farklı olarak tarayıcı seviyesinde isteğin tamamlanamadığını gösterir. Kod seviyesinde bunun kesin nedeni henüz kanıtlanmış değildir. Özellikle `/api/v5/analysis` içindeki çok sayıdaki CHPP çağrısı ve M6'nın M7→M8 değerlendirmeli araması nedeniyle request süresi / bağlantı kopması / hosting timeout ihtimali araştırılmalıdır.

Bu nedenle gerçek web oturumunda **sunucu tarafı motor logları ve request timing** eklenmeden hatanın belirli bir motordan kaynaklandığı varsayılmamalıdır.

## Questionnaire etkisi — önemli bug

Web kullanıcıdan üç veri almaktadır:

1. `CoachStyle` — Dengeli / Hücum / Defans
2. `TeamSpirit` — takım ruhu
3. `MatchImportance` — Normal / PIC / MOTS

Bu veriler session'a kaydedilip `MatchQuestionnaire` olarak pipeline'a aktarılmaktadır.

### Önceki durum

- **TeamSpirit:** M7'de `MatchState.TeamSpirit` üzerinden midfield ratingini etkiliyordu.
- **MatchImportance:** `RatingContext.Attitude` üzerinden M7/base rating hesabına giriyordu.
- **CoachStyle:** questionnaire'dan okunmasına rağmen `QuestionnaireRatingAdjuster.Apply(...)` hiçbir canlı pipeline noktasında çağrılmıyordu. Dolayısıyla kullanıcı Hücum veya Defans seçse bile M3→M10 sonucu bu seçimden etkilenmiyordu.

### Yapılan düzeltme

CoachStyle artık pipeline içinde M7'ye açık şekilde taşınmaktadır:

```text
Questionnaire.Coach
       ↓
MatchState.CoachStyle
       ↓
M7 Regional Rating
       ↓
M8 Matchup / Chance
       ↓
M9 Prediction
       ↓
M10 Final Decision
```

M7'de coach etkisi TeamSpirit'ten ayrı uygulanır; böylece TeamSpirit'in midfield etkisi **iki kez uygulanmaz**.

Kullanılan coach katsayıları:

| Coach | Attack | Defence |
|---|---:|---:|
| Dengeli | ×1.00 | ×1.00 |
| Hücum | ×1.08 | ×0.89 |
| Defans | ×0.92 | ×1.14 |

Bu katsayıların tarihsel gerçek maç sonuçlarıyla calibration'ı ayrıca yapılmalıdır; burada amaç öncelikle kullanıcı seçiminin motor zincirine gerçekten bağlanmasını sağlamaktır.

## Sonraki düzeltme kapısı

Canlı hatanın teşhis ve çözümü için sıra:

```text
1. Questionnaire etkisini pipeline'a bağla             ✅
2. CoachStyle'ın M7→M10 sonucunu regression ile doğrula 🔄
3. /api/v5/analysis request timing ekle                🔜
4. M3 → M10 gerçek server-side motor logları ekle     🔜
5. CHPP / pipeline timeout noktasını tespit et         🔜
6. Frontend'de raw "Failed to fetch" yerine açıklayıcı hata göster 🔜
7. Offline Regression                                  🔜
8. Docker Build / Deploy                               🔜
9. Gerçek CHPP web analizi                             🔜
```

**Kural:** Önce hata gözlemlenebilir hale getirilecek, sonra performans/timeout ve motor davranışı düzeltilecek. M3 temel katsayıları bu hata nedeniyle değiştirilmemelidir.

---

# Regression / Build durumu

Son derlemede eski pipeline sözleşmelerinden kaynaklanan üç compile hatası tespit edildi ve düzeltildi:

1. M10 içindeki eksik `RankedCandidate` tipi
2. `TacticalCandidate.StructuralScore` yerine mevcut contract'a uygun structural chance kullanımı
3. `RatingContext.TeamAttitude` yerine doğru `RatingContext.Attitude` kullanımı

Düzeltmelerden sonra `v5` branch üzerinde yeni GitHub Actions çalışması başlatıldı.

**README güncellemesi sırasında son workflow hâlâ çalışıyordu; bu nedenle burada build/deploy PASS ilan edilmemektedir.** Sonraki kapı:

```text
Offline Regression
      ↓
Docker Build
      ↓
Azure Deploy
      ↓
Gerçek Web Analizi
      ↓
M3→M10 Motor Log doğrulaması
      ↓
Final XI + Individual Order doğrulaması
```

---

# Regression kapısı

Offline CHPP fixture üzerinde korunması gereken temel zincir:

```text
M3 → M4 → M5 → M6 → M7 → M7.2 → M8 → M9 → M10
```

Her katmanda mümkün olduğunca:

- input count
- output count
- invalid candidate count
- missing data
- duplicate candidate
- CandidateId continuity
- deterministik tekrar üretilebilirlik
- PASS / FAIL

kontrol edilmelidir.

Bir aşama FAIL olursa sonraki aşamaya geçilmemelidir.

---

# Mevcut geliştirme sırası

```text
M3 LOCK
 ↓
M4 VALIDATED / LIVE
 ↓
M5 VALIDATED / LIVE
 ↓
M6 ACTIVE
 ↓
M7 INTEGRATED
 ↓
M7.2 INTEGRATED
 ↓
M8 INTEGRATED
 ↓
M9 INTEGRATED
 ↓
M10 INTEGRATED
 ↓
WEB FINAL XI CONNECTION        ← TAMAMLANDI
 ↓
WEB MOTOR LOGS                 ← TAMAMLANDI
 ↓
INDIVIDUAL ORDER UI            ← TAMAMLANDI
 ↓
QUESTIONNAIRE → M7 WIRING     ← TAMAMLANDI
 ↓
BUILD / DEPLOY VALIDATION      ← DEVAM EDİYOR
 ↓
LIVE ANALYSIS TIMEOUT / ERROR DIAGNOSTICS
 ↓
M10 MULTI-CANDIDATE RANKING
 ↓
Gerçek maç calibration
 ↓
M9/M10 historical calibration
 ↓
Daha derin global optimization
```

---

# Nihai mimari hedef

V5'in nihai hedefi tek bir büyük formül yerine, her katmanın görevini net biçimde ayırdığı deterministik ve izlenebilir bir motor zinciridir:

```text
M1  Veri / CHPP
 │
 ├──────────────→ M2  Rakip Analizi
 │
 └──────────────→ M3  Oyuncu Analizi
                         ↓
                  M4 Formasyon Adayları
                         ↓
                  M5 XI Adayları
                         ↓
                  M6 Global XI + Behaviour
                         ↓
                  M7 Rating Simulation
                         ↓
                  M7.2 Tactical Scenario
                         ↓
                  M8 Matchup / Chance
                         ↓
                  M9 Match Prediction
                         ↓
                  M10 Final Decision
                         ↓
                  WEB Final XI
```

**Temel prensip:** Web arayüzü kendi başına XI seçmez. Motorların final çıktısını gösterir. M3 → M10 zinciri tamamlandıkça her katmanın çıktısı bir sonraki katmanın girdisi olur.
