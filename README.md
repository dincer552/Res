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
M5   XI / pozisyon adayları            🟡 genişletilecek
 ↓
M6   Global XI + behaviour             🟡 ACTIVE / yeniden tasarlanacak
 ↓
M7   Regional Rating Scenario          ✅ INTEGRATED
 ↓
M7.2 Advanced Tactical Scenario        ✅ INTEGRATED
 ↓
M8   Chance / Matchup                  ✅ INTEGRATED
 ↓
M9   Match Prediction                  ✅ INTEGRATED
 ↓
M10  Candidate Review / Search Gate    🔜 YENİ MİMARİ
 ↓
M6-B ikinci arama döngüsü               🔜 YENİ MİMARİ
 ↓
Candidate Database #2                  🔜 YENİ MİMARİ
 ↓
M11  Final Decision                    🔜 YENİ MİMARİ
 ↓
WEB  Final XI + Individual Order       ✅ CONNECTED
```

### En önemli mevcut durum

- `AnalysisService` artık eski doğrudan XI yerleştirme yolunu kullanmıyor.
- `/api/v5/analysis` çağrısı M3 → M4 → M5 → M6 → M7 → M7.2 → M8 → M9 → M10 zincirinden geçiyor.
- Web arayüzünde gösterilen **önerilen kendi 11'i M10'un `FinalPlan` çıktısından geliyor.**
- M10 seçiminin ardından final lineup aynı zamanda saha üzerinde çiziliyor.
- Her oyuncunun saha kutusunda **RP değeri + Individual Order** gösteriliyor.
- Individual Order seçenekleri motor tarafından seçilen final lineup üzerinde korunuyor.
- Eski sabit/doğrudan XI placement yolu final önerinin kaynağı olmaktan çıkarıldı.
- Yeni hedef, tek bir `BestCandidate` üzerinde karar vermek yerine **çoklu diziliş + XI + davranış adaylarını rakibe karşı tekrar tekrar değerlendiren bir arama döngüsü** kurmaktır.

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

### Yeni hedef

M5 artık canlı analizde formasyon başına yalnızca 6 adayla sınırlı kalmamalıdır. Hedef:

```text
M4
 ↓
6+ legal formation
 ↓
M5
 ↓
formasyon başına yaklaşık 20 güçlü XI
 ↓
TOPLAM yaklaşık 120 XI adayı
```

Amaç bütün Cartesian uzayı körlemesine üretmek değildir. M5, exact assignment + beam yaklaşımıyla her formasyon için yeterli çeşitlilikte güçlü XI üretmelidir.

**Durum:** LIVE / validated; aday havuzunun genişletilmesi planlanıyor.

### M6 — Global Search / Behaviour Optimization

M6'nın mevcut hali M5 adayları üzerinde Individual Order davranışlarını beam search ile optimize eder ve tek bir `BestCandidate` üretir. Mevcut kodda her XI için Normal baseline oluşturulup legal Individual Order varyasyonları downstream M7/M8 evaluator'ına gönderilir. fileciteturn706file0L2-L3

Bu yaklaşım korunacak ancak görev genişletilecektir.

#### M6-A — İlk global arama

```text
M5
 ↓
~120 XI
 ↓
M6-A
 ↓
XI + Individual Order varyasyonları
 ↓
M7 → M7.2 → M8
 ↓
CandidateEvaluation
 ↓
Candidate Database #1
 ↓
TOP 100
```

M6-A'nın amacı yalnızca RP/suitability açısından en güçlü XI'yi seçmek değildir. **Rakibe karşı downstream performansı yüksek adayları korumaktır.**

M6 değerlendirme sırasında şu bilgiler adayla birlikte tutulmalıdır:

- Formation
- XI / player assignment
- Individual Orders
- M7 regional ratings
- M7.2 tactical scenario
- M8 chance / matchup
- tactical score
- candidate identity

#### M6-B — İkinci arama döngüsü

M10'un ilk değerlendirmesinden sonra M6'ya geri dönülebilen ikinci bir search pass kurulacaktır.

Amaç:

```text
M10'un ilk tur sonucu
        ↓
hangi bölgeler / dizilişler / davranışlar güçlü-zayıf?
        ↓
M6-B
        ↓
TOP 100 aday çevresinde yeni varyasyonlar
        ↓
özellikle alternatif dizilişleri tekrar dene
        ↓
Candidate Database #2
```

Bu döngü **local refinement + formation diversification** şeklinde çalışmalıdır. M6'nın ilk turda 2-5-3 seçmesi, ikinci turda diğer formasyonların tekrar denenmesini engellememelidir.

**Durum:** ACTIVE; yeni çok turlu arama mimarisine geçirilecek.

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

### M10 — Candidate Review / Search Gate

M10'un mevcut hali daha önce değerlendirilmiş candidate'lar arasından deterministik final karar katmanıdır. Composite yapıda tactical score + prediction win probability + structural chance kullanılır. Mevcut pipeline ise M6'nın yalnızca tek `BestCandidate` sonucunu M10'a gönderdiği için gerçek çoklu aday sıralaması yapmamaktadır. fileciteturn705file0L2-L3

Bu mimari değiştirilecektir.

Yeni M10 görevi:

1. Candidate Database #1 içindeki güçlü adayları incelemek.
2. Diziliş çeşitliliğini koruyarak ilk finalist havuzunu oluşturmak.
3. En iyi adayın neden seçildiğini ve hangi bölgelerde avantaj/dezavantaj bulunduğunu üretmek.
4. Gerekirse M6-B ikinci arama döngüsünü tetikleyecek search feedback üretmek.
5. **Tek başına final XI'yi kilitlememek.**

Mevcut M10 yaklaşım karşılaştırması (Normal / PIC / MOTS) korunabilir; ancak final mimaride bu karar da candidate database üzerinden yapılmalıdır.

**Durum:** INTEGRATED / mevcut hali geçici. Yeni görev: multi-candidate review + search gate.

### M11 — Final Decision Engine

M11 yeni mimarinin gerçek final karar katmanıdır.

M11, Candidate Database #2'deki adayları aynı standartla karşılaştırır ve tek bir final plan seçer.

Final aday değerlendirmesi en az şu bilgileri içermelidir:

- formation
- XI
- Individual Orders
- M7 regional ratings
- M7.2 tactical state
- M8 matchup / chance
- M9 win / draw / loss probabilities
- tactical score
- structural score
- robustness / stability
- formation diversity context

Örnek final tablo:

```text
#1  3-5-2   Win 61.4%   MID 6.82   composite 0.781
#2  2-5-3   Win 59.8%   MID 5.73   composite 0.764
#3  4-5-1   Win 57.9%   MID 7.31   composite 0.752
#4  3-4-3   Win 55.2%   MID 6.44   composite 0.731
```

Buradaki örnek sayılar mimariyi anlatmak içindir; gerçek değerler motorlardan üretilecektir.

**Durum:** PLANLANDI.

---

# YENİ V5 ANA YOL HARİTASI — MULTI-CANDIDATE SEARCH LOOP

## Neden bu mimariye geçiyoruz?

Gerçek testlerde aynı veya benzer girdilerle M10'un tekrar tekrar 2-5-3'e yönelmesi ve bazı senaryolarda orta saha ratinginin düşük kalmasına rağmen orta sahayı güçlendiren alternatif dizilişlerin yeterince yarışa sokulmaması önemli bir mimari sinyal vermiştir.

Temel problem yalnızca bir rating katsayısı değildir. Mevcut pipeline'da M6 tek bir `BestCandidate` üretip M7 → M8 → M9 → M10'a bu adayı taşımaktadır. Bu durumda M10'un önünde 3-5-2, 4-5-1, 3-4-3 gibi alternatifleri gerçek rakip karşılaştırmasıyla yeniden yarıştıracak geniş bir candidate database bulunmamaktadır. fileciteturn705file0L2-L3

Yeni prensip:

> **En yüksek RP / suitability değerine sahip XI'yi seçmek değil, seçilen rakibe karşı kazanma ihtimalini ve taktik dayanıklılığı en yüksek XI + diziliş + davranış kombinasyonunu aramak.**

## Hedef akış

```text
M1 / CHPP veri
      ↓
M2 Rakip Analizi
      ↓
M3 Oyuncu Analizi
      ↓
M4 Formasyon Adayları
      ↓
M5 Geniş XI Havuzu
      │
      │  ~20 / formation
      │  ~120 toplam
      ↓
M6-A Global Search
      │
      │  XI + Individual Orders
      ↓
Candidate Database #1
      │
      │  TOP 100
      ↓
M7 Regional Rating
      ↓
M7.2 Tactical Scenario
      ↓
M8 Matchup / Chance
      ↓
M9 Match Prediction
      ↓
M10 Candidate Review
      │
      ├──────── güçlü/zayıf bölgeler
      ├──────── formation karşılaştırması
      └──────── search feedback
                 ↓
              M6-B
                 │
                 │ yeni varyasyonlar
                 │ alternatif dizilişler
                 │ güçlü aday çevresi
                 ↓
Candidate Database #2
      │
      │  TOP finalist pool
      ↓
M11 Final Decision
      ↓
🏆 FINAL XI + Formation + Individual Orders
      ↓
WEB
```

## Candidate Database kuralları

Database kalıcı bir öğrenme modeli olmak zorunda değildir; ilk aşamada **tek analiz oturumu içindeki adayların izlenebilir değerlendirme havuzu** olarak uygulanacaktır.

Her kayıt en az:

```text
CandidateId
Formation
Lineup
Player assignments
Individual Orders
M5 Suitability
M5 Structural Score
M6 Tactical Score
M7 Regional Rating
M7.2 Tactical Scenario
M8 Matchup
M8 Structural Chance
M9 Win / Draw / Loss
Composite Score
Search Round
Parent Candidate / Mutation Source
```

şeklinde tutulmalıdır.

### Database #1

M5 → M6-A sonrasında en iyi **100 aday** korunur.

Ancak diversity zorunludur. Örneğin 100 adayın 90'ının aynı formasyondan gelmesi istenmez. İlk aşamada aday havuzunda formation diversity için bir üst sınır / quota uygulanacaktır; kesin oran gerçek benchmark sonuçlarına göre ayarlanacaktır.

### Database #2

M6-B ikinci aramasından sonra oluşur.

Burada:

- Database #1'in üst adayları
- M10'un tespit ettiği kritik bölgeler
- alternatif formasyonlar
- Individual Order varyasyonları
- güçlü adayların yakın komşuları

birlikte değerlendirilir.

## Orta saha problemi için özel prensip

Sistem bir adayda:

```text
MID düşük
```

gördüğünde doğrudan "MID katsayısını yükselt" şeklinde davranmayacaktır.

Bunun yerine aynı rakibe karşı:

```text
2-5-3
3-5-2
3-4-3
4-5-1
4-4-2
5-3-2
...
```

gibi legal formasyonları ve bunların farklı XI'lerini gerçek M7 → M8 → M9 zincirinden geçirecektir.

Örneğin hipotetik olarak:

```text
2-5-3  MID 5.73  → Win 44%
3-5-2  MID 6.80  → Win 57%
4-5-1  MID 7.10  → Win 55%
```

çıktısı oluşursa sistem 2-5-3'ü sırf RP/suitability nedeniyle seçmemelidir. **Rakibe karşı downstream sonucu daha iyi olan aday kazanmalıdır.**

Bu örnek değerler gerçek motor sonucu değildir; beklenen karar mantığını gösterir.

## Search loop durma kuralları

İkinci arama sonsuz döngüye girmemelidir.

Başlangıç kriterleri:

- maksimum 2 global search round
- her round için sabit candidate budget
- aynı `Signature` tekrar değerlendirilmez
- yeni round anlamlı skor artışı üretmiyorsa erken durabilir
- M11 her durumda mevcut en iyi valid candidate pool üzerinden karar verebilir

İleride gerçek benchmark verisi oluştuğunda:

- adaptive beam width
- adaptive candidate budget
- marginal gain stop
- formation diversity quota

geliştirilebilir.

## Determinizm

Aynı:

- CHPP snapshot
- questionnaire
- opponent data
- player pool

ile aynı arama sınırları altında motor aynı final sonucu üretmelidir.

Her candidate'ın `Signature` değeri unique olmalı ve search round / parent bilgisi izlenebilir olmalıdır.

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

Yeni hedefte bu akış:

```text
Kullanıcı → Analiz
       ↓
CHPP + seçilen lig maçı
       ↓
M3
       ↓
M4
       ↓
M5 geniş candidate pool
       ↓
M6-A
       ↓
DB #1
       ↓
M7 → M7.2 → M8 → M9
       ↓
M10 Review
       ↓
M6-B
       ↓
DB #2
       ↓
M11
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

Yeni multi-candidate mimaride bu log yapısı genişletilmelidir:

- M5: formation başına aday sayısı / toplam aday
- M6-A: evaluated / retained / top formations
- DB #1: 100 aday ve formation dağılımı
- M7/M7.2/M8/M9: candidate değerlendirme özeti
- M10: ilk finalist sıralaması
- M6-B: yeni varyasyon sayısı / skor kazanımı
- DB #2: finalist havuzu
- M11: final sıralama ve seçilen XI

Mevcut kutu analiz sonrasında gerçek API cevabındaki pipeline bilgilerini okuyarak motorların durumunu göstermektedir.

## Saha üzerindeki oyuncu kutuları

Final lineup'ındaki oyuncuların saha kutularında artık:

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
- `match-select.js` → analiz öncesinde yaklaşan lig maçını 1. soru olarak seçtirir ve mevcut 3 questionnaire sorusunu 2-4. sorular olarak devam ettirir.
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

## Maç seçimi questionnaire akışı

Analiz başlamadan önce yaklaşan **lig maçları** CHPP'den alınır ve ilk soru olarak kullanıcıya gösterilir.

Yeni questionnaire sırası:

```text
1. Rakip / lig maçı seç
2. Teknik direktör
3. Takım ruhu
4. Bu maçta hangi yaklaşım olsun?
```

4. soru mevcut `TeamAttitude` değerleriyle:

- Normal
- PIC • Rahat
- MOTS • Çok önemli

olarak çalışır.

İlerleyen mimaride bu soru için **Otomatik** seçeneği de desteklenmelidir. Auto seçildiğinde motor Normal / PIC / MOTS'u adayların gerçek M7→M9 sonuçlarıyla karşılaştırarak seçmelidir; kullanıcı adına sabit eşik kullanılmamalıdır.

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
M3 → M4 → M5 → M6-A → M7 → M7.2 → M8 → M9 → M10 → M6-B → M11
```

Her katmanda mümkün olduğunca:

- input count
- output count
- invalid candidate count
- missing data
- duplicate candidate
- CandidateId continuity
- formation diversity
- search round continuity
- deterministik tekrar üretilebilirlik
- PASS / FAIL

kontrol edilmelidir.

Bir aşama FAIL olursa sonraki aşamaya geçilmemelidir.

---

# Yeni geliştirme sırası

```text
M3 LOCK
 ↓
M4 VALIDATED / LIVE
 ↓
M5 aday havuzunu genişlet (~20 / formation)
 ↓
M6-A multi-XI / behaviour search
 ↓
Candidate Database #1 / TOP 100
 ↓
M7 / M7.2 / M8 / M9 candidate evaluation
 ↓
M10 multi-candidate review
 ↓
M6-B ikinci search loop
 ↓
Candidate Database #2
 ↓
M11 final selector
 ↓
WEB final XI
 ↓
Offline Regression
 ↓
Docker Build / Deploy
 ↓
Gerçek CHPP benchmark
 ↓
Historical calibration
 ↓
Adaptive search / deeper optimization
```

## Uygulama öncelikleri

### Faz 1 — Candidate pool

- [ ] M5'i formasyon başına yaklaşık 20 adaya genişlet.
- [ ] Toplam candidate budget'i kontrol altında tut.
- [ ] Aynı oyuncu / aynı slot / duplicate candidate korumalarını sürdür.
- [ ] Formation diversity bilgisini candidate metadata'ya ekle.

### Faz 2 — M6-A

- [ ] M6'nın tek `BestCandidate` odaklı sonucunu candidate collection'a dönüştür.
- [ ] Her candidate için M7→M8 evaluator sonucunu sakla.
- [ ] Top 100 database oluştur.
- [ ] Candidate Signature ile duplicate önle.
- [ ] Search round ve parent candidate bilgisini sakla.

### Faz 3 — M10 Review

- [ ] M10'a tek aday değil candidate database ver.
- [ ] Formation bazlı karşılaştırma üret.
- [ ] MID / DEF / ATT dengesizliğini matchup ile birlikte raporla.
- [ ] En güçlü adayların hangi bölgelerde kazandığını/kaybettiğini çıkar.
- [ ] M6-B için feedback üret.

### Faz 4 — M6-B

- [ ] İlk 100 adaydan yeni varyasyonlar üret.
- [ ] Alternatif formasyonları özellikle koru.
- [ ] Individual Order komşuluklarını yeniden ara.
- [ ] Skor artışı sağlamayan tekrarları ele.
- [ ] İkinci candidate database oluştur.

### Faz 5 — M11

- [ ] Database #2'yi final candidate havuzu olarak kullan.
- [ ] Tactical + prediction + structural + robustness skorlarını birleştir.
- [ ] Final ranking üret.
- [ ] Tek bir final XI + formation + Individual Orders döndür.
- [ ] Webün yalnızca M11 final sonucunu göstermesini sağla.

### Faz 6 — Calibration

- [ ] Gerçek maç sonuçlarıyla M8/M9/M10/M11 kalibrasyonu.
- [ ] Hangi rating farklarının gerçekten kazanma olasılığını artırdığını ölç.
- [ ] Formation diversity quota'sını gerçek sonuçlara göre ayarla.
- [ ] Search budget / beam width'i benchmark sonuçlarına göre optimize et.

---

# Nihai mimari hedef

V5'in nihai hedefi tek bir büyük formül yerine, her katmanın görevini net biçimde ayırdığı deterministik, izlenebilir ve **çok adaylı arama yapan** bir motor sistemidir:

```text
M1  Veri / CHPP
 │
 ├──────────────→ M2  Rakip Analizi
 │
 └──────────────→ M3  Oyuncu Analizi
                         ↓
                  M4 Formasyon Adayları
                         ↓
                  M5 Geniş XI Havuzu
                         ↓
                  M6-A Global Search
                         ↓
                  Candidate DB #1
                         ↓
                  TOP 100
                         ↓
                  M7 Rating Simulation
                         ↓
                  M7.2 Tactical Scenario
                         ↓
                  M8 Matchup / Chance
                         ↓
                  M9 Match Prediction
                         ↓
                  M10 Candidate Review
                         ↓
                  M6-B Second Search
                         ↓
                  Candidate DB #2
                         ↓
                  M11 Final Decision
                         ↓
                  WEB Final XI
```

### Temel prensipler

1. **Web arayüzü XI seçmez.** Motorun final çıktısını gösterir.
2. **M3 temel oyuncu katsayıları lock'tadır.** Yeni gerçek veri olmadan değiştirilmez.
3. **M5 tek bir XI üretmez.** Yeterli çeşitlilikte candidate pool üretir.
4. **M6 tek bir karar noktası değildir.** Search engine olarak çalışır ve ikinci turda tekrar kullanılabilir.
5. **M7→M9 candidate bazında değerlendirme yapar.** Rakip bilgisi aramanın merkezindedir.
6. **M10 ilk tur hakemidir, final kilidi değildir.**
7. **M11 final selector'dır.**
8. **Aynı girdiler deterministik aynı sonucu üretmelidir.**
9. **Candidate database yalnızca analiz süresince tutulabilir; ilk aşamada kalıcı ML modeli değildir.**
10. **Öncelik rating katsayılarını sürekli değiştirmek değil, doğru aday uzayını taramaktır.**

Bu mimari tamamlandığında sistemin temel sorusu:

> **“Benim en güçlü dizilişim hangisi?”**

yerine:

> **“Bu rakibe, bu maç koşullarına ve bu oyuncu havuzuna karşı kazanma ihtimalini en çok artıran diziliş + XI + Individual Order kombinasyonu hangisi?”**

olacaktır.
