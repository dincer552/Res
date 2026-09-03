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

# 9 — UYGULAMA ROADMAP'I

Mevcut formation competition çalışmalarından sonra match-engine araştırma sırası:

```text
FAZ 1   M9 W/D/L tutarlılığı                         ✅
FAZ 2   M6-A formation-aware search                 🔜 / devam
FAZ 3   DB1 gerçek formation depth                   🔜
FAZ 4   M10 formation leaderboard                   🔜
FAZ 5   M6-B exploration + refinement               🔜
FAZ 6   DB2 formation depth                         🔜
FAZ 7   M11 final comparison                        🔜
FAZ 8   Web finalist / alternatif görünüm            🔜
FAZ 9   Offline regression                          🔜

MATCH ENGINE RESEARCH TRACK

R1  Chance allocation araştırması                   🔜
R2  Exclusive/open chance modeli                    🔜
R3  Sector distribution + AIM/AOW calibration       🔜
R4  Pressing chance suppression                     🔜
R5  Counter Attack extra-chance model               🔜
R6  Long Shots event model                          🔜
R7  Play Creatively + SE model                      🔜
R8  CHPP Specialty pipeline                         🔜
R9  Specialty event engine                          🔜
R10 Specialty ↔ tactic interaction                  🔜
R11 Weather ↔ specialty effects                     🔜
R12 Historical calibration dataset                  🔜
R13 Event-by-event M8/M9 engine                     🔜
R14 Full Monte Carlo match simulation                🔜
R15 Offline regression / real-match validation       🔜
```

### Uygulama sırası için karar

**R1–R7 tamamlanmadan specialty katsayılarını production rating sistemine gömmeyeceğiz.**

Önce şans ve taktik çekirdeğinin doğru ayrıştırılması, ardından specialty event katmanının eklenmesi hedefleniyor.

---

# 10 — ÇALIŞMA PRENSİBİ

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
