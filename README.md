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

### PHASE A — CHPP Specialty veri bağlantısı ✅

İlk uygulama aşaması tamamlandı.

```text
CHPP players XML
      ↓
Specialty alanı okunuyor
      ↓
PlayerSpecialty enum
      ↓
Player model
      ↓
M3 oyuncu veri zinciri
```

Desteklenen değerler:

```text
0 = None
1 = Technical
2 = Quick
3 = Powerful
4 = Unpredictable
5 = Head
```

Uygulama notları:

- `Player` modeline `PlayerSpecialty Specialty` alanı eklendi.
- CHPP `players` endpoint'indeki `Specialty` alanı okunuyor.
- Geçersiz/bilinmeyen değerler güvenli şekilde `None` kabul ediliyor.
- Bu aşamada specialty'ye **herhangi bir yapay rating bonusu uygulanmıyor**.
- Specialty henüz M7/M7.2/M8/M9 event çözümlemesine etki etmiyor; yalnızca doğru verinin motor zincirine girmesi sağlandı.

İlgili uygulama commitleri:

```text
33ab339a  Player modeline CHPP Specialty alanı eklendi
7a0b2b6a  CHPP Specialty verisi Player modeline bağlandı
```

### PHASE B — M3 Specialty-aware Player Profile ✅

M3 artık specialty bilgisini yalnızca ham alan olarak taşımıyor; sonraki motorların kullanabileceği **yapısal bir specialty context** üretiyor.

```text
Player + Specialty
       ↓
M3 Player Profile
       ├─ position candidates
       ├─ primary / secondary position
       └─ specialty context
```

M3 specialty context içerisinde şu etkileşim alanları ayrı tutuluyor:

- Special Event context
- Weather interaction
- Counter Attack interaction
- Pressing interaction
- Quick Event interaction
- Header interaction
- Play Creatively interaction

Önemli sınır: Bu alanlar **henüz rating skoruna bonus/ceza uygulamıyor**. M3 sadece `hangi specialty hangi mekanizma ile ilişkilendirilebilir` bilgisini kanonik profile bağlıyor. Böylece M5/M6/M7.2/M8/M9 daha sonra aynı veriyi tekrar çıkarmak zorunda kalmayacak.

İlgili uygulama commit:

```text
f2e6ef61  M3 specialty-aware Player Profile
```

### PHASE C — M5/M6 specialty-aware candidate 🔜

Bir sonraki kod aşamasında specialty context, XI ve individual order adaylarının değerlendirilmesine bağlanacak. Burada da doğrudan keyfi rating bonusu verilmek yerine specialty'nin adayın oynadığı rol ve rakip/maç koşullarıyla ilişkisi taşınacak.

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

### Hedef mimari

```text
CHPP Player
    ↓
Specialty
    ↓
M3 Player Profile ✅
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

```text
Centre    35%
Left      25%
Right     25%
Set Piece 15%
```

2026 akademik çalışmanın 1 milyon gerçek maçlık veri setinde normal saldırı dağılımı daha ayrıntılı olarak incelenmiş ve set-piece türleri ayrıca ayrıştırılmıştır. Bu veri V5 için önemli bir calibration referansıdır; kesin Hattrick server formülü olarak kabul edilmeyecektir.

### Exclusive / open chance hedefi

Araştırılan match-engine yapısında normal şanslar:

```text
5 exclusive Home
5 exclusive Away
5 open/shared
```

mantığıyla ele alınır. Bu nedenle V5'te yapay continuous chance tabanı yerine gerçek chance allocation'a daha yakın discrete/probabilistic bir model hedeflenecektir.

### Sektör dağılımı

Normal baseline:

```text
Left     ≈ 25%
Centre   ≈ 35%
Right    ≈ 25%
SP       ≈ 15%
```

Maçta gerçekleşen sayılar bu oranların birebir kopyası değildir; küçük örneklem nedeniyle gerçek dağılım doğal olarak değişebilir.

### AIM / AOW

```text
Normal
  ↓
AIM → wing attacks'ın bir kısmı middle'a
AOW → middle attacks'ın bir kısmı wing'e
```

Taktik seviyesi arttıkça dönüşüm oranı değişir. Kaynaklardaki aralıklar ile akademik ampirik model arasındaki farklar calibration konusu olarak tutulacaktır.

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

```text
normal middle/wing attack
        ↓
LS conversion
        ↓
shooter vs goalkeeper
```

ayrı event tipi olarak modellenmelidir.

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

Kritik prensip: **önce veri → sonra mekanizma → sonra calibration → en son production katsayısı.**

---

# ARAŞTIRMA / UYGULAMA SIRASI

```text
R1  Chance allocation araştırması
R2  Exclusive/open chance modeli
R3  Sector distribution + AIM/AOW calibration
R4  Pressing chance suppression
R5  Counter Attack extra-chance model
R6  Long Shots event model
R7  Play Creatively + SE model
R8  CHPP Specialty pipeline              → PHASE A ✅
R9  Specialty event engine
R10 Specialty ↔ tactic interaction
R11 Weather ↔ specialty effects
R12 Historical calibration dataset
R13 Event-by-event M8/M9 engine
R14 Full Monte Carlo match simulation
R15 Offline regression / real-match validation
```

### Şu anki durum

```text
PHASE A  CHPP Specialty → Player/M3 data      ✅ TAMAMLANDI
PHASE B  M3 Specialty-aware profile           ✅ TAMAMLANDI
PHASE C  M5/M6 specialty-aware candidate      🔜 SONRAKİ
PHASE D  M7.2 specialty ↔ tactic interaction  🔜
PHASE E  M8 Special Event engine              🔜
PHASE F  M9 event-based resolution            🔜
PHASE G  historical calibration               🔜
PHASE H  full Monte Carlo                    🔜
PHASE I  offline regression                  🔜
```

Production'a geçmeden önce her aşama offline regression ile korunacaktır.
