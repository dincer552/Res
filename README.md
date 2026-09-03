# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 canlı analizde M3 → M11 çoklu aday zincirine sahiptir. Ana hedef; tüm legal formasyonları yeterli search derinliğiyle yarıştırmak ve M7 → M7.2 → M8 → M9 hattını gerçek Hattrick maç motoru davranışına mümkün olduğunca yaklaştırmaktır.

### Güncel motor zinciri

```text
M3    Oyuncu Analizi
 ↓
M4    Formasyon Üretimi
 ↓
M5    11 Adayı Üretimi
 ↓
M6-A  Global Arama / formation-aware search
 ↓
DB1   Candidate Database #1
 ↓
M7    Bölgesel Rating
 ↓
M7.2  Taktik Senaryo
 ↓
M8    Şans / Eşleşme
 ↓
M9    Maç Tahmini + W/D/L + Monte Carlo
 ↓
M10   Formasyon Kararı / Competition
 ↓
M6-B  İkinci Arama / Refinement + Exploration
 ↓
DB2   Candidate Database #2
 ↓
M11   Final Seçici
 ↓
WEB   Final XI + Individual Order
```

## FORMATION COMPETITION / ANTI-LOCK HEDEFİ

V5 hiçbir formasyona önceden avantaj veya ceza vermemelidir. Her legal formasyon kendi search bütçesi içinde güçlü XI + Individual Order adayları üretmeli ve aynı rakip koşullarında M7 → M7.2 → M8 → M9 hattından geçirilmelidir.

```text
Her legal formation
      ↓
kendi search budget
      ↓
kendi aday havuzu
      ↓
M7 → M7.2 → M8 → M9
      ↓
aynı composite kriterler
      ↓
M10 formation competition
      ↓
M6-B refinement + exploration
      ↓
DB2
      ↓
M11 final comparison
```

Temel kurallar:

1. Her legal formasyon yarışta kalmalı.
2. Bir formasyon yalnızca tek "koruma adayı" ile temsil edilmemeli.
3. M5 formasyon başına geniş XI havuzu üretmeye devam etmeli.
4. M6-A global beam search bir formasyonun diğerlerini erken boğmasına izin vermemeli.
5. M6-A mümkün olduğunca formation-aware / per-formation search budget kullanmalı.
6. DB1 search diversity'nin yerine geçmemeli; yalnızca koruyucu ikinci katman olmalı.
7. M10 gerçek çoklu aday + formasyon karşılaştırması yapmalı.
8. M10 sonucu M6-B'ye refinement/exploration feedback vermeli.
9. M6-B az temsil edilen formasyonları yeniden aramalı.
10. DB2 aynı çeşitliliği korumalı.
11. M11 tüm finalistleri aynı kriterlerle karşılaştırmalı.
12. Finalde formasyon bazlı karşılaştırma ve search-depth bilgisi gösterilmeli.

## YENİ ARAŞTIRMA PROGRAMI — HATTRICK MATCH ENGINE

Bu bölüm **hemen kodlanacak özellik listesi değil**, araştırma ve doğrulama yol haritasıdır. Önce Hattrick mekanikleri kaynaklardan ve gerçek maçlardan çıkarılacak, sonra mevcut V5 ile karşılaştırılacak, ancak yeterli kanıt oluştuğunda motorlara uygulanacaktır.

Üç ana konu özellikle takip edilecek:

```text
1. ÖZEL YETENEKLER / SPECIALTIES
2. TAKTİKLER / TACTICS
3. ŞANS DAĞILIMI / CHANCE ALLOCATION
```

Bu üç konu birbirinden ayrı incelenecek fakat sonunda tek bir maç-event çekirdeğinde birleştirilecek.

---

# 1 — ÖZEL YETENEKLER / SPECIALTIES

### Amaç

CHPP'den gelen oyuncu specialty bilgisini M3 → M11 zincirine taşıyıp specialty'lerin gerçek etkisini **düz rating bonusu vermeden**, bağlama göre modellemek.

### Araştırma hedefleri

- Technical
- Quick
- Powerful
- Head
- Unpredictable
- Support
- pozisyona göre specialty etkileri
- specialty vs specialty karşılaşmaları
- specialty + individual order etkileşimi
- specialty + weather etkileşimi
- specialty + tactic etkileşimi
- pozitif ve negatif Special Event'ler
- specialty event'lerinin gol olasılığına etkisi
- specialty'nin normal 7 ratingden bağımsız etkileri

### Şimdiden doğrulanan önemli prensip

Specialty çoğu durumda doğrudan `+rating` değildir. Örneğin Head, Quick veya Unpredictable oyuncuya keyfi `+0.x rating` eklemek yerine Special Event, taktik katkısı, hava durumu veya rakip specialty etkileşimi üzerinden modellenmelidir.

### CHPP

CHPP `players` / `playerDetails` verilerinde Specialty alanı bulunur.

```text
0 = No specialty
1 = Technical
2 = Quick
3 = Powerful
4 = Unpredictable
5 = Head
```

### Hedef mimari

```text
CHPP Player
    ↓
Specialty
    ↓
M3 Player Profile
    ↓
M5/M6 lineup + behaviour evaluation
    ↓
M7 rating context
    ↓
M7.2 tactic interaction
    ↓
M8 event inputs
    ↓
M9 Special Event resolution
```

### Uygulama kuralı

Kesin katsayı bulunamayan specialty etkileri uydurma sabit bonus olarak kodlanmayacak. Kaynakta belirtilen mekanizma ayrı tutulacak; katsayı gerekiyorsa historical calibration ile belirlenecek.

---

# 2 — TAKTİKLER / TACTICS

Hattrick'te Normal dışında altı temel taktik takip edilecek:

```text
Pressing
Counter Attack
Attack in the Middle (AIM)
Attack on Wings (AOW)
Long Shots (LS)
Play Creatively (PC)
```

### Araştırma hedefi

Her taktik için dört ayrı şeyi ayırmak:

```text
A) Tactical Level nasıl oluşuyor?
B) Hangi oyuncu skill'leri kullanılıyor?
C) Normal ratinglere hangi yan etkiler uygulanıyor?
D) Maçta hangi chance/event üretim mekanizması değişiyor?
```

### Araştırmada doğrulanan temel davranışlar

**Pressing**

- potansiyel normal şansları azaltır,
- Special Event'leri doğrudan azaltmaz,
- oyuncu Defending/Stamina kapasitesi önemlidir,
- Powerful oyuncular pressing'e özel katkı sağlayabilir.

**Counter Attack**

- rakibin kaçırdığı normal ataktan ekstra hücum üretir,
- normal şansların yerine geçen basit bir dağılım değildir,
- CA kullanan takım midfield'de yaklaşık %7 ceza alır,
- savunmacıların Passing + Defending değerleri CA seviyesinde önemlidir,
- Quick oyuncular da CA seviyesine katkı sağlayabilir.

**Attack in the Middle (AIM)**

- toplam normal şans sayısını değiştirmek yerine wing → middle dönüşümü yapar,
- taktik seviyesi outfield Passing üzerinden oluşur,
- wing defence tarafında dezavantaj oluşturur.

**Attack on Wings (AOW)**

- middle → wing dönüşümü yapar,
- taktik seviyesi outfield Passing üzerinden oluşur,
- central defence tarafında dezavantaj oluşturur.

**Long Shots (LS)**

- middle/wing normal ataklarının bir kısmını long-shot eventine çevirir,
- Scoring + Set Pieces kullanır; Scoring daha ağırdır,
- shooter ile goalkeeper doğrudan karşılaştırılır,
- pressing long-shot fırsatını da engelleyebilir.

**Play Creatively (PC)**

- specialty ve diğer özel event mekanizmalarını daha fazla kullanmayı hedefler,
- normal chance redistribution değildir,
- takım savunmasında dezavantajı vardır,
- specialty yoğun takım için daha anlamlıdır.

### V5'te hedeflenen taktik katmanı

```text
M7 Regional Ratings
       ↓
M7.2 Tactical Level + Tactical Side Effects
       ↓
Tactical Chance/Event Engine
       ↓
M8 Chance Resolution
       ↓
M9 Match Prediction
```

Mevcut M7.2 yapısal olarak bu taktikleri içeriyor; ancak bazı dönüşüm katsayılarının gerçek maç verisiyle calibration ihtiyacı var. Bu nedenle mevcut yapı **research-backed structure**, nihai gerçek motor değildir.

---

# 3 — ŞANS DAĞILIMI / CHANCE ALLOCATION

Bu alan şu anda V5 için en kritik araştırma başlıklarından biridir.

### Gerçek Hattrick mantığını hedefleyen akış

```text
MIDFIELD
   ↓
POSSESSION / OPEN-CHANCE ALLOCATION
   ↓
EXCLUSIVE + OPEN CHANCES
   ↓
NORMAL CHANCE COUNT
   ↓
LEFT / CENTRE / RIGHT / SET PIECE
   ↓
TACTIC CONVERSION
   ↓
ATTACK vs DEFENCE
   ↓
GOAL RESOLUTION
```

### Araştırmada kullanılan baseline

Normal chance dağılımı için mevcut kaynaklarda yaklaşık:

```text
Centre   35%
Left     25%
Right    25%
Set Piece 15%
```

baseline kullanılır.

2026 akademik çalışmanın 1 milyon gerçek maçlık veri setinde normal saldırı dağılımı daha ayrıntılı olarak incelenmiş ve set-piece türleri ayrıca ayrıştırılmıştır. Bu veri V5 için önemli bir calibration referansıdır; kesin Hattrick server formülü olarak kabul edilmeyecektir.

### Exclusive / open chance hedefi

Araştırılan match-engine yapısında normal şanslar:

```text
5 exclusive Home
5 exclusive Away
5 open/shared
```

mantığıyla ele alınır. Exclusive şans rakibe aktarılmak yerine kazanılamadığında kaybolabilir; open şanslar midfield/possession karşılaştırmasına göre dağıtılır.

Bu nedenle V5'te:

> `chance volume = 0.35 + 0.65 × possession`

gibi yapay continuous bir taban yerine, gerçek chance allocation'a daha yakın discrete/probabilistic bir model hedeflenecektir.

### Sektör dağılımı

Normal baseline:

```text
Left     ≈ 25%
Centre   ≈ 35%
Right    ≈ 25%
SP       ≈ 15%
```

Ancak maçta gerçekleşen sayılar bu oranların birebir kopyası değildir. Örneğin 9 normal şansın 4'ünün merkezden gelmesi veya 2-2-2 gibi bir dağılım görülmesi normal rastlantısal sonuçlardır.

### AIM / AOW

```text
Normal
  ↓
AIM → wing attacks'ın bir kısmı middle'a
AOW → middle attacks'ın bir kısmı wing'e
```

Taktik seviyesi arttıkça dönüşüm oranı değişir. Kaynaklarda yaklaşık AIM `%15–30`, AOW `%20–40` aralıkları verilir; 2026 akademik modelinde farklı ampirik aralıklar kullanıldığı için bu değerler calibration konusu olarak tutulacaktır.

### Counter Attack

CA normal chance distribution'ın basit bir yeniden ağırlıklandırması değildir.

```text
Opponent normal chance
        ↓
failed/missed
        ↓
CA check
        ↓
successful
        ↓
additional attack
```

### Pressing

Pressing normal chance dağılımını başka sektöre taşıyan bir mekanizma olarak değil, potansiyel normal şansları kıran/söndüren bir mekanizma olarak incelenecektir.

### Long Shots

LS:

```text
normal middle/wing attack
        ↓
LS conversion
        ↓
shooter vs goalkeeper
```

olarak ayrı event tipi şeklinde modellenmelidir.

---

# 4 — ÜÇ KONUNUN BİRLEŞTİRİLMESİ

Nihai hedef:

```text
                 PLAYER SKILLS
                      │
                      ▼
              POSITION CONTRIBUTION
                      │
                      ▼
                7 TEAM RATINGS
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
   TACTICAL ENGINE         SPECIALTY ENGINE
          │                       │
          └───────────┬───────────┘
                      ▼
             CHANCE ALLOCATION
                      │
                      ▼
             CHANCE / EVENT TYPE
                      │
          ┌───────────┼────────────┐
          ▼           ▼            ▼
       NORMAL        TACTICAL     SPECIAL
       CHANCE        EVENTS       EVENTS
          │           │            │
          └───────────┴────────────┘
                      ▼
              ATTACK RESOLUTION
                      ▼
                     M9
```

Burada kritik prensip:

> **Rating, chance ve event aynı matematik katmanı değildir.**

M7 takımın bölgesel ratinglerini üretir. M7.2 taktik durumunu üretir. M8 hangi fırsatların oluşabileceğini ve eşleşmesini modeller. M9 ise bu olayları gol/W-D-L sonucuna dönüştürür.

---

# 5 — MEVCUT V5 İLE ARAŞTIRMA SONRASI KARŞILAŞTIRMA

### Mevcut durum

```text
M7
  7 bölgesel rating var

M7.2
  Taktik yapısı var
  Pressing / CA / AIM / AOW / LS / PC yapısal olarak mevcut

M8
  Midfield share + sector matchup var
  Chance distribution var

M9
  xG + Poisson W/D/L var
  1000x Monte Carlo var
```

### Eksik / geliştirilecek ana parçalar

```text
[1] Gerçek discrete chance allocation
[2] Exclusive/open chance mekanizması
[3] Taktiklerin chance/event dönüşüm katsayılarının calibration'ı
[4] CA ekstra chance üretimi
[5] Pressing chance suppression
[6] LS ayrı shooter-vs-GK event çözümü
[7] PC special-event üretimi
[8] Specialty event engine
[9] Specialty ↔ opponent specialty interactions
[10] Specialty ↔ tactic interactions
[11] Weather ↔ specialty skill effects
[12] Gerçek maçlardan historical calibration
```

Mevcut M9 Monte Carlo, seçilmiş M9 sonucunu varyasyonlarla örnekleyen bir simülasyondur; henüz Hattrick'in tam event-by-event maç motorunun birebir simülasyonu değildir.

---

# 6 — GERÇEK MAÇLARLA İLK REGRESSION VERİSİ

2026-09-03 incelemesinde S4MSUNFC'nin iki gerçek maçı başlangıç referansı olarak not edildi.

### S4MSUNFC 1–2 Kara Spor

```text
Final: 1–2
Normal chance / dağılım ekranı:
S4M       1
Kara     10

S4M:
Right     1

Kara:
Right     4
Centre    2
Left      2
Other     1
Special   1
```

### Kaymakspor 0–4 S4MSUNFC

```text
Final: 0–4
Chance:
Kaymaks   2
S4M       9

S4M:
Right     2
Centre    4
Left      2
Other     1
Special   0
```

Bu maçlar **model katsayısı olarak doğrudan hard-code edilmeyecek**. İlk regression/calibration örnekleri olarak tutulacak.

Özellikle şu değişkenler karşılaştırılacak:

```text
Possession
Normal chance count
Left/Centre/Right chance count
Set-piece / Other chance count
Special Event count
Tactic
Tactic level
7 regional ratings
Final score
```

Yeni gerçek maçlar geldikçe bu veri havuzu büyütülecek.

---

# 7 — ARAŞTIRMA KAYNAKLARI / BOOKMARKS

Bu kaynaklar V5 match-engine araştırmasının temel referans listesi olarak korunacaktır.

### Hattrick Wiki — ana referans

- https://wiki.hattrick.org/wiki/Match_engine
- https://wiki.hattrick.org/wiki/Regular_chances
- https://wiki.hattrick.org/wiki/Tactics
- https://wiki.hattrick.org/wiki/Rules
- https://wiki.hattrick.org/wiki/Specialty
- https://wiki.hattrick.org/wiki/Special_event
- https://wiki.hattrick.org/wiki/New_Framework_for_Specialities_%26_Special_Events

### Specialty araştırmaları

- https://wiki.hattrick.org/wiki/Technical
- https://wiki.hattrick.org/wiki/Quick
- https://wiki.hattrick.org/wiki/Powerful
- https://wiki.hattrick.org/wiki/Head
- https://wiki.hattrick.org/wiki/Unpredictable
- https://wiki.hattrick.org/wiki/Support

### Taktik araştırmaları

- https://wiki.hattrick.org/wiki/Pressing
- https://wiki.hattrick.org/wiki/Counter-attacks
- https://wiki.hattrick.org/wiki/Attack_in_the_middle
- https://wiki.hattrick.org/wiki/Attack_on_wings
- https://wiki.hattrick.org/wiki/Long_shots
- https://wiki.hattrick.org/wiki/Play_creatively

### CHPP

- https://wiki.hattrick.org/wiki/CHPP_Development/XML/players
- https://wiki.hattrick.org/wiki/CHPP_Development/XML/playerDetails

CHPP, oyuncu specialty bilgisinin kaynağı olarak kullanılacak. Specialty değeri tahmin edilmeyecek veya oyuncu adına göre çıkarılmayacaktır.

### Akademik match-engine araştırması — 2026

**Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning**

DOI:
https://doi.org/10.1016/j.entcom.2026.101131

ScienceDirect:
https://www.sciencedirect.com/science/article/pii/S1875952126000534

Bu çalışma özellikle değerlidir çünkü gerçek CHPP verisinden **250 değişken / 1 milyon maç** içeren veri seti kullanır ve regular chances, tactics, specialities, set-pieces ve match outcomes arasındaki ilişkileri Bayesian network ile inceler.

### Hattrick Context Pack

`rmagasi/hattrick-context-pack`

Bu kaynak Hattrick mekaniklerini doğrulamak ve V5'e kural katmanı sağlamak için kullanılacaktır. Model eğitimi olarak kullanılmayacaktır.

---

# 8 — ARAŞTIRMA KURALI: KESİN / ARAŞTIRILMIŞ / CALIBRATED AYRIMI

V5 match-engine çalışmalarında üç bilgi seviyesi açıkça ayrılacaktır:

```text
OFFICIAL / DOCUMENTED
  Hattrick'in açıkladığı kural veya mekanizma

RESEARCHED / COMMUNITY
  Wiki, Unwritten Manual, uzun süreli community araştırmaları

CALIBRATED / EMPIRICAL
  Gerçek maç verisinden çıkarılan katsayı veya ilişki
```

Bir katsayının kaynağı bilinmiyorsa onu "kesin Hattrick formülü" olarak sunmayacağız.

Özellikle hidden match-engine mekaniklerinde amaç:

```text
kaynak
  ↓
mekanizma
  ↓
formül / katsayı
  ↓
real-match validation
  ↓
regression
  ↓
production
```

olacaktır.

---

# 9 — ARAŞTIRMA ROADMAP'I

```text
R1  Chance allocation araştırması
R2  Exclusive/open chance modeli
R3  Sector distribution + AIM/AOW calibration
R4  Pressing chance suppression
R5  Counter Attack extra-chance model
R6  Long Shots event model
R7  Play Creatively + SE model
R8  CHPP Specialty pipeline
R9  Specialty event engine
R10 Specialty ↔ tactic interaction
R11 Weather ↔ specialty effects
R12 Historical calibration dataset
R13 Event-by-event M8/M9 engine
R14 Full Monte Carlo match simulation
R15 Offline regression / real-match validation
```

R1–R7 araştırılmadan production specialty katsayıları hard-code edilmeyecek.

---

# 10 — UYGULAMA ROADMAP'I / MOTORLARIN GERÇEK GENİŞLETİLMESİ

Bu bölüm kodlama sırasını tanımlar. Buradaki amaç yeni motor sayısını artırmak değil, mevcut M3 → M11 zincirinin taşıdığı state'i ve hesaplama kombinasyonlarını kontrollü biçimde genişletmektir.

## PHASE A — CHPP SPECIALTY VERİ KATMANI

**Durum: ✅ Başlatıldı**

Hedef:

```text
CHPP players
   ↓
Specialty
   ↓
Player model
   ↓
M3 Player Profile
```

Yapılan ilk değişiklikler:

- `Player` modeline `PlayerSpecialty` alanı eklendi.
- CHPP `players` XML içindeki `Specialty` alanı parse edilmeye başlandı.
- M3 `PlayerAnalysisProfile` specialty bilgisini taşıyor.
- Specialty henüz ratinge bonus olarak uygulanmıyor.
- Mevcut oyuncu constructor'ları backward-compatible tutuldu; specialty verilmemiş fixture'lar `None` kabul ediliyor.

CHPP oyuncu dokümantasyonunda Specialty değerleri 0–5 olarak tanımlıdır: No specialty, Technical, Quick, Powerful, Unpredictable, Head specialist. citeturn358224search0turn358224search1

**Kontrol:** PHASE A'nın sonraki CI çalışmasında model/parse regression doğrulanacak.

## PHASE B — M3 SPECIALTY-AWARE OYUNCU ANALİZİ

**Durum: 🔜**

M3'ün amacı specialty'yi oyuncu için görünür context haline getirmektir; specialty doğrudan `+rating` olarak eklenmeyecek.

```text
Player
 ├─ skills
 ├─ form
 ├─ experience
 └─ specialty
        ↓
M3
 ├─ position suitability
 ├─ specialty context
 └─ candidate positions
```

M3 aynı oyuncunun farklı pozisyonlardaki specialty kullanım potansiyelini sonraki motorlara taşıyacak.

## PHASE C — M4/M5 ADAY UZAYINI BÜYÜTME

**Durum: 🔜**

Amaç bir formasyonu yalnızca ratingi en yüksek tek XI ile temsil etmemek.

```text
M4 legal formation
       ↓
M5
 ├─ skill fit
 ├─ position fit
 ├─ specialty composition
 └─ daha geniş XI candidate pool
```

Plan:

- formation başına mevcut yaklaşık 20 XI havuzu calibration sonucuna göre artırılacak;
- aynı formasyon içinde farklı specialty dağılımlarına sahip XI'ler korunacak;
- specialty yoğunluğu tek başına avantaj/ceza olmayacak, sonraki tactical/event katmanı için state olarak taşınacak;
- M5 adayları deterministic ve bounded kalacak.

## PHASE D — M6-A SEARCH KOMBINASYONLARINI BÜYÜTME

**Durum: 🔜**

M6 artık yalnızca bireysel emir kombinasyonlarını değil, specialty taşıyan XI'lerin davranış uzayını arayacak.

```text
XI
 ↓
Player position
 ↓
Individual order
 ↓
Tactical scenario
 ↓
M7 / M7.2 / M8 / M9 evaluation
```

M6'nın formation-aware yapısı korunacak. Search budget formasyon bazında çalışacak ve bir formasyon diğerlerini erken boğamayacak.

Planlanan kombinasyon boyutları:

```text
Oyuncu
× Pozisyon
× Individual Order
× Formasyon
× Taktik
× Match Context
```

Ancak tüm çarpanlar aynı anda brute-force edilmeyecek; beam/search ile bounded exploration yapılacak.

## PHASE E — M7 RATING CONTEXT

**Durum: 🔜**

M7 temel 7 bölgesel rating üretmeye devam edecek. Specialty burada ancak kaynak bunu gerçekten rating contribution olarak destekliyorsa uygulanacak.

Ana prensip:

> Special Event etkisini 7 rating içine zorla sıkıştırma.

M7 rating, M7.2 tactical state ve M8/M9 event state ayrı tutulacak.

## PHASE F — M7.2 TACTIC × SPECIALTY

**Durum: 🔜**

Mevcut M7.2 zaten Pressing, Counter Attack, AIM, AOW, Long Shots ve Play Creatively yapısal olarak içeriyor. Sonraki adım specialty'nin ilgili taktiğe gerçek etkisini eklemek.

```text
M7 ratings
    +
Specialty profile
    +
Team tactic
    +
Opponent context
        ↓
M7.2 tactical state
```

Örnek hedefler:

```text
Powerful × Pressing
Quick × Counter Attack
Technical × Head opponent
Unpredictable × Play Creatively
Specialty × Weather
```

Kesin katsayılar calibration olmadan üretim skoruna gömülmeyecek.

## PHASE G — M8 GERÇEK CHANCE ALLOCATION

**Durum: 🔜 / kritik**

Mevcut continuous chance index zamanla discrete/probabilistic chance engine ile değiştirilecek.

Hedef:

```text
Midfield
   ↓
5 exclusive + 5 open/shared
   ↓
normal chance count
   ↓
sector distribution
   ↓
AIM/AOW conversion
   ↓
Pressing suppression
   ↓
CA extra chances
   ↓
LS conversion
   ↓
set pieces
```

Böylece M8 yalnızca `StructuralChanceIndex` üretmek yerine hangi tip fırsatların oluştuğunu temsil eden daha zengin bir state üretecek.

## PHASE H — M9 EVENT-BY-EVENT MAÇ MOTORU

**Durum: 🔜**

M9'un görevi sadece xG üretmekten çıkıp event resolution'a genişletilecek.

```text
M8 chance/event state
       ↓
Normal Chance
Tactical Event
Special Event
Set Piece Event
       ↓
Attack vs Defence / GK
       ↓
Goal / Miss / Turnover
       ↓
W/D/L
```

Monte Carlo burada çalışacak.

Önemli performans kuralı:

- M6'nın her adayında 1000x pahalı simülasyon çalıştırılmayacak.
- Önce analitik M8/M9 filtreleme yapılacak.
- Monte Carlo özellikle finalist/elite adaylarda yoğunlaştırılacak.

## PHASE I — M10 YENİ MAÇ MOTORUYLA FORMASYON YARIŞI

**Durum: 🔜**

M10 artık yalnızca rating ağırlıklı finalistleri değil, gerçek chance/event çıktılarıyla formasyonları yarıştıracak.

```text
Formation A
  candidate 1..N
        ↘
         M10
        ↗
Formation B
  candidate 1..N
```

M10 karşılaştırmasında:

```text
Tactical Score
+ Win Probability
+ Matchup quality
+ Chance quality
+ Event compatibility
```

gibi ölçütler calibration sonucuna göre kullanılacak.

## PHASE J — M6-B TARGETED EXPLORATION + REFINEMENT

**Durum: 🔜**

M6-B tüm DB1'i körlemesine tekrar aramak yerine M10 bilgisini kullanarak:

```text
M10 leaderboard
      ↓
under-explored formations
      ↓
weak/uncertain finalist branches
      ↓
specialty/tactic combinations
      ↓
M6-B targeted search
```

yapacak.

Amaç:

> M10'un kazananı ilan etmeden önce henüz yeterince test edilmemiş güçlü kombinasyonları tekrar aramak.

## PHASE K — DB1 / DB2 SEARCH DEPTH

**Durum: 🔜 / devam**

Candidate DB yalnızca sonuç depolayan bir havuz değil, search diversity kontrol katmanı olacak.

```text
Her legal formation
      ↓
minimum candidate depth
      ↓
global top-up
      ↓
DB1
      ↓
M10
      ↓
M6-B targeted exploration
      ↓
DB2
```

Minimum formation depth, total capacity ve beam budget birlikte calibration edilecek.

## PHASE L — M11 FINAL COMPARISON

**Durum: 🔜**

M11:

```text
DB2
 ↓
all legal formations
 ↓
all eligible finalists
 ↓
aynı final criteria
 ↓
BEST XI + orders
```

Formasyon seçimi yalnızca tek bir rating karşılaştırmasına dayandırılmayacak.

## PHASE M — FULL MONTE CARLO + REAL MATCH REGRESSION

**Durum: 🔜**

Nihai doğrulama:

```text
Historical Hattrick match
        ↓
Observed:
- possession
- chances
- sectors
- tactics
- special events
- final score
        ↓
V5 prediction
        ↓
Regression / calibration
```

Monte Carlo regression kontrolleri:

- simulation count
- W/D/L sum
- score distribution
- scenario/event distribution
- deterministic repeated run
- real-match sanity checks

---

# 11 — KOMBİNASYON UZAYI HEDEFİ

V5'in yeni hedefi motor sayısını artırmak değil, her motorun taşıdığı bilgi ve aday uzayını katmanlı olarak büyütmektir.

```text
CHPP PLAYER
      │
      ├── skills
      ├── form
      ├── experience
      └── SPECIALTY
             │
             ▼
M3 PLAYER ANALYSIS
             │
             ▼
M4 FORMATION
             │
             ▼
M5 XI CANDIDATES
             │
             ├── position variants
             ├── specialty composition
             └── XI alternatives
             │
             ▼
M6-A SEARCH
             │
             ├── individual orders
             ├── tactical branches
             └── formation-aware exploration
             │
             ▼
M7 RATINGS
             │
             ▼
M7.2 TACTICAL STATE
             │
             ├── tactic
             ├── tactical level
             ├── side effects
             └── specialty interaction
             │
             ▼
M8 CHANCE ENGINE
             │
             ├── possession
             ├── exclusive/open
             ├── sector distribution
             ├── tactic conversion
             ├── CA / LS / pressing
             └── set pieces
             │
             ▼
M9 EVENT ENGINE
             │
             ├── normal goals
             ├── tactical events
             ├── special events
             ├── weather
             └── Monte Carlo
             │
             ▼
M10 FORMATION COMPETITION
             │
             ▼
M6-B TARGETED SEARCH
             │
             ▼
M11 FINAL
```

Kural:

> Her yeni bilgi katmanı mümkün olduğunca bir önceki katmanda state olarak taşınacak; bir sonraki motor bu state'i yeniden hesaplamak yerine kullanacaktır.

Bu sayede kombinasyon sayısı kontrollü biçimde artarken aynı hesaplamanın tekrar tekrar yapılması engellenecek.

---

# 12 — UYGULAMA GÜVENLİK KURALLARI

Yeni match-engine özellikleri eklenirken:

1. Önce veri modeli.
2. Sonra parser.
3. Sonra motor state'i.
4. Sonra aday üretimi.
5. Sonra evaluator.
6. Sonra regression.
7. En son production scoring.

Aynı commit içinde ilgisiz UI değişikliği yapılmayacak.

DOM'da mevcut JS kodunun sonradan güncellediği elemanlar silinmeyecek; gizleme gereken yerlerde CSS kullanılacak.

Ayrıca:

```text
NO UNBOUNDED BRUTE FORCE
NO UNDOCUMENTED HARD-CODED HATTRICK FORMULA
NO SPECIALTY => FLAT RATING BONUS
NO M8/M9 DUPLICATE CALCULATION
NO FORMATION STARVATION
```

---

# 13 — ÇALIŞMA DURUMU / SONRAKİ ADIM

Şu anda başlangıç uygulaması **PHASE A** üzerindedir.

```text
PHASE A  CHPP Specialty → Player → M3 Profile     ✅ kodlandı
PHASE B  M3 specialty-aware analysis              🔜
PHASE C  M4/M5 candidate expansion                🔜
PHASE D  M6 search expansion                      🔜
PHASE E  M7 rating context                        🔜
PHASE F  M7.2 tactic × specialty                  🔜
PHASE G  M8 real chance allocation                🔜
PHASE H  M9 event-by-event                        🔜
PHASE I  M10 formation competition                🔜
PHASE J  M6-B targeted exploration                🔜
PHASE K  DB1/DB2 depth                             🔜
PHASE L  M11 final comparison                      🔜
PHASE M  Monte Carlo + regression                 🔜
```

Bir faz, CI/offline regression doğrulanmadan tamamlanmış kabul edilmeyecek.

---

# 14 — ÇALIŞMA PRENSİBİ

Bu araştırma tek seferlik değildir.

Yeni gerçek Hattrick maçları, match report verileri, CHPP verileri veya yeni community/academic araştırmaları geldikçe:

```text
Yeni veri
   ↓
README / research notes
   ↓
mekanizma karşılaştırması
   ↓
V5 mevcut model ile fark analizi
   ↓
calibration adayı
   ↓
offline regression
   ↓
ancak sonra production kod
```

Amaç yalnızca "Hattrick'e benzeyen" bir motor yapmak değil;

> **Hattrick'in gözlemlenebilir match-engine davranışını kaynak + gerçek maç verisi + regression ile adım adım yeniden kurmak.**

Bu bölüm ileride tekrar tekrar incelenmek üzere kalıcı araştırma notu olarak tutulacaktır.
