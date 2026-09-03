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

## OFFLINE FIXTURE TEST CHECKPOINT — 2026-09-03

Kullanılan fixture:

```text
HattrickAI_V5_CHPP_FullOffline_2026-09-01T08-49-54-690Z.json
```

Test maçı:

```text
S4MSUNFC
vs
Zeytinburnu Sahil Spor

06.09.2026 15:00
Zeytinburnu Sahil Spor — S4MSUNFC
Deplasman
```

Fixture, CHPP ham verisi ile birlikte M3/M7 zincirinin kullandığı normalize verileri içeriyor. Gelecek maçın ve rakibin son maçının bilgileri de fixture içinde mevcut. fileciteturn731file1L20-L20 fileciteturn731file6L310-L310

### Test sonucu

```text
CHPP oyuncu verisi                 ✅
Specialty ham verisi               ✅
M3 position candidates              ✅
M3 primary/secondary profile        ✅
M4 legal formation                 ✅
M5 önerilen XI                     ✅
Rakip son lineup                   ✅
M7 regional ratings                ✅

M8 discrete allocation             ⏳ kod üzerinden doğrulama
M9 xG / W-D-L                      ⏳ kod üzerinden doğrulama
M10/M11 final competition           ⏳ CI full-pipeline doğrulaması
```

Fixture'da M3 normalize oyuncu profillerinde pozisyon adayları ve primary/secondary skorlar mevcut; örneğin Antonín Vašica için primary `IM-C`, secondary `IM-L` ve skorlar 14.94 / 13.86 olarak geliyor. fileciteturn733file0L25-L29

Rakibin son lineup verisi de player ID, position code, role, behaviour ve ratingStars alanlarıyla mevcut. fileciteturn733file0L97-L125

Fixture'daki V5 analizinde bizim önerilen formasyon `3-5-2`; önerilen oyuncular ve bireysel ratingler normalize çıktıya yazılmış durumda. fileciteturn731file4L57-L76 fileciteturn731file4L79-L112

### Specialty veri doğrulaması

CHPP ham oyuncu XML'inde `Specialty` değerleri gerçek şekilde mevcut. Örnekte `Specialty=3` ve `Specialty=4` oyuncuları bulunuyor; dolayısıyla yeni `PlayerSpecialty` veri bağlantısının test fixture'ı gerçek specialty çeşitliliği içeriyor. fileciteturn733file2L146-L146 fileciteturn733file5L298-L300

**Sonuç:** Fixture, Specialty → M3 → formation/XI → regional rating hattını test etmek için yeterli. Ancak gerçek M8/M9 event/chance sonuçlarını doğrulayacak geçmiş maç event dağılımı bu fixture'ın normalize çıktısında bulunmuyor. Bu nedenle M8/M9 katsayıları bu dosyadan tek başına kalibre edilmeyecek.

---

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

Bu bölüm araştırma ve doğrulama yol haritasıdır. Önce Hattrick mekanikleri kaynaklardan ve gerçek maçlardan çıkarılacak, sonra mevcut V5 ile karşılaştırılacak; yeterli kanıt oluştuğunda production katsayısı uygulanacaktır.

Üç ana konu özellikle takip edilecek:

```text
1. ÖZEL YETENEKLER / SPECIALTIES
2. TAKTİKLER / TACTICS
3. ŞANS DAĞILIMI / CHANCE ALLOCATION
```

Bu üç konu birbirinden ayrı incelenecek fakat sonunda tek bir maç-event çekirdeğinde birleştirilecek.

---

# 1 — ÖZEL YETENEKLER / SPECIALTIES

### PHASE A — CHPP Specialty veri bağlantısı ✅

```text
CHPP players XML
      ↓
Specialty alanı
      ↓
PlayerSpecialty enum
      ↓
Player model
      ↓
M3 veri zinciri
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
- Specialty'ye yapay rating bonusu uygulanmıyor.

İlgili commitler:

```text
33ab339a  Player modeline CHPP Specialty alanı eklendi
7a0b2b6a  CHPP Specialty verisi Player modeline bağlandı
```

### PHASE B — M3 Specialty-aware Player Profile ✅

M3 artık specialty bilgisini sonraki motorların kullanabileceği yapısal context olarak taşıyor.

```text
Player + Specialty
       ↓
M3 Player Profile
       ├─ position candidates
       ├─ primary / secondary position
       └─ specialty context
```

Specialty context alanları:

- Special Event context
- Weather interaction
- Counter Attack interaction
- Pressing interaction
- Quick Event interaction
- Header interaction
- Play Creatively interaction

Bu aşamada rating bonusu/cezası yoktur; M3 mekanizma context'i üretir.

İlgili commit:

```text
f2e6ef61  M3 specialty-aware Player Profile
```

### PHASE C — M5/M6 specialty-aware candidate 🔜

Specialty context'in XI ve Individual Order seçimlerine bağlanması planlanıyor. Burada specialty doğrudan `+rating` olarak uygulanmayacak; rol, rakip ve maç koşullarına göre aday uygunluğuna bağlanacak.

---

# 2 — MAÇ MOTORU ESKİLİK ANALİZİ / REVİZYON ÖNCELİĞİ

Kod incelemesinden sonra V5'in en önemli teknik borcu M7 → M8 → M9 maç çekirdeğinde toplandı.

### Durum değerlendirmesi

```text
M3-M6 / formation search       🟢 güçlü
M10-M11 / final competition    🟢 güçlü
M7 / regional rating           🟡 korunacak + calibration
M7.2 / tactics                 🟡 yapı doğru, event bağlantısı eksik
M8 / chance model              🟡 structural revision in progress
M9 / xG-WDL core               🟡 M8 allocation'a bağlı
Monte Carlo                    🟡 çekirdek düzeldikten sonra yeniden kalibre
```

### Kritik mimari kararı

Takım seçme/search motoru artık ana risk alanı değildir. M6-A → DB1 → M10 → M6-B → DB2 → M11 formation competition yapısı korunacaktır.

Asıl geliştirme odağı:

```text
M7 ratings
 ↓
Possession / chance allocation
 ↓
Discrete chances
 ↓
Tactic conversion / suppression
 ↓
Attack vs defence
 ↓
Special Events
 ↓
M9 W/D/L
```

M7 tamamen yeniden yazılmayacak. Mevcut pozisyon katkı yapısı korunacak, araştırma ile doğrulanmayan katsayılar daha sonra historical calibration ile değerlendirilecek.

---

# 3 — ŞANS DAĞILIMI / CHANCE ALLOCATION — ÖNCELİKLİ FAZ

V5'in önceki M8 modeli midfield share ile sektör kalitesini çarpıp tek bir continuous `StructuralChanceIndex` üretiyordu. Bu yapı gerçek match-engine mantığını fazla basitleştiriyordu.

Yeni hedef:

```text
MIDFIELD
   ↓
POSSESSION
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

### Araştırılan structural baseline

```text
5 exclusive Home
5 exclusive Away
5 open/shared
```

Bu yapı production server formülü olarak kabul edilmiyor. Şimdilik V5'e chance allocation katmanı eklemek için **structural baseline** olarak kullanılıyor.

### PHASE C1 — M8 discrete chance allocation ✅ temel katman

Yeni `M8ChanceAllocationEngine` eklendi.

Mevcut model artık:

```text
M8
 ↓
midfield share
 ↓
5 exclusive + 5 open structural pool
 ↓
open chances ownership = midfield share
 ↓
own / opponent expected regular chance count
```

şeklinde veri üretiyor.

Yeni kayıt:

```text
DiscreteChanceAllocation
```

alanları:

```text
OwnExclusive
OpponentExclusive
OpenChancePool
OwnOpenExpected
OpponentOpenExpected
OwnRegularChanceExpected
OpponentRegularChanceExpected
```

### PHASE C2 — M9 chance-volume migration ✅ ilk bağlantı

M9 artık önceki:

```text
0.35 + 0.65 × midfieldShare
```

continuous chance-floor yaklaşımını kullanmıyor.

M9 chance volume artık M8'in allocation çıktısından geliyor:

```text
M8 OwnRegularChanceExpected
          ↓
M9 OwnChanceVolume
          ↓
sector attack quality
          ↓
xG
          ↓
Poisson W/D/L
```

Bu hâlâ calibration-neutral structural geçiştir; kesin toplam chance formülü daha sonra tarihsel maç verisiyle belirlenecek.

Uygulama commit:

```text
2f5b8a62  M9 şans hacmini M8 discrete allocation katmanına bağla
```

---

# 4 — TAKTİKLER / TACTICS

Hattrick'te Normal dışında altı temel taktik takip edilecek:

```text
Pressing
Counter Attack
Attack in the Middle (AIM)
Attack on Wings (AOW)
Long Shots (LS)
Play Creatively (PC)
```

### V5 hedefi

```text
M7 Regional Ratings
       ↓
M7.2 Tactical Level + Side Effects
       ↓
Tactical Chance/Event Engine
       ↓
M8 Chance Resolution
       ↓
M9 Match Prediction
```

Mevcut M7.2 korunacak; fakat taktiklerin M8'de gerçek chance/event dönüşümüne bağlanması sonraki fazlarda yapılacak.

---

# 5 — SPECIAL EVENT MOTORU

Hedef:

```text
Player Specialty
       ↓
Event Eligibility
       ↓
Event Type
       ↓
Weather / Tactic / Opponent interaction
       ↓
Event Resolution
       ↓
Goal / No Goal
```

Specialty doğrudan takım ratingine yapıştırılmayacaktır.

---

# 6 — M8 → M9 YENİ HEDEF MİMARİSİ

```text
M7 Regional Ratings
        ↓
Possession / Midfield
        ↓
M8 Chance Allocation
        │
        ├── Exclusive chances
        ├── Open chances
        └── Sector distribution
        ↓
M7.2 Tactical conversion
        │
        ├── AIM / AOW
        ├── Pressing
        ├── CA
        └── LS
        ↓
Chance / Event Type
        │
        ├── Normal chance
        ├── Tactical event
        └── Special event
        ↓
Attack vs Defence / Shooter vs GK
        ↓
M9 xG + W/D/L
        ↓
Monte Carlo
```

Kritik kural: **M9 artık kendi başına chance üretmemeli. Chance üretiminin sahibi M8 olmalıdır.**

---

# 7 — SIRADAKİ FAZ: HISTORICAL CHANCE CALIBRATION

Offline fixture testi bize veri zincirinin kopmadığını ve M3 → M7 tarafının gerçek CHPP girdileriyle çalışabildiğini gösterdi. Fakat bu fixture gerçek maç-event/chance dağılımını içermediği için M8'in toplam chance sayısını bu dosyaya bakarak kalibre etmek doğru değil.

Bu nedenle sıradaki iş doğrudan **PHASE D**:

```text
PHASE D — Chance total historical calibration

Gerçek maçlar
      ↓
Midfield / possession
      ↓
Toplam normal chance
      ↓
Home / Away exclusive
      ↓
Open/shared
      ↓
M8 structural output
      ↓
Gerçek maç ile karşılaştırma
      ↓
Calibration dataset
```

İlk hedef production katsayısı yazmak değil; mevcut M8'in hatasını ölçmek:

```text
Predicted total chances
vs
Observed total chances

Predicted ownership
vs
Observed ownership
```

### PHASE D için veri gereksinimi

Her maç için mümkün olduğunca:

```text
Home / Away
Midfield rating
Possession / chance share
Total normal chances
Chance sector distribution
Set-piece / Other
Final score
Tactic
Formation
```

Special-event calibration ayrı tutulacak; önce normal chance üretim mekanizması stabilize edilecek.

### PHASE D test kuralı

```text
M8 calibration ≠ M9 calibration
```

Önce M8'in **kaç şans ürettiğini** ve **kime dağıttığını** doğrulayacağız. Sonra M9'un bu şansları gole çevirme modelini kalibre edeceğiz.

---

# 8 — UYGULAMA SIRASI

```text
PHASE A   CHPP Specialty → Player             ✅
PHASE B   M3 Specialty-aware Profile          ✅
PHASE C1  M8 discrete chance allocation      ✅
PHASE C2  M9 chance-volume migration          ✅

PHASE D   Chance total historical calibration 🔜 NEXT
PHASE E   Sector distribution calibration     🔜
PHASE F   AIM / AOW chance conversion         🔜
PHASE G   Pressing suppression                🔜
PHASE H   Counter Attack extra chances        🔜
PHASE I   Long Shots event conversion         🔜
PHASE J   Play Creatively + SE                🔜
PHASE K   Specialty event engine              🔜
PHASE L   Specialty ↔ tactic / weather        🔜
PHASE M   M9 event-based resolution           🔜
PHASE N   Historical calibration              🔜
PHASE O   Full Monte Carlo                    🔜
PHASE P   Offline regression / real matches   🔜
```

### Production kuralı

Hiçbir araştırma katsayısı yalnızca tek fixture'dan production'a alınmayacak.

```text
DATA
 ↓
MECHANISM
 ↓
OBSERVED vs PREDICTED
 ↓
CALIBRATION
 ↓
REGRESSION TEST
 ↓
PRODUCTION COEFFICIENT
```
