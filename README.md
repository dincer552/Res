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
M8 / chance model              🔴 revizyon gerekli
M9 / xG-WDL core               🔴 M8 sonrası yeniden bağlanacak
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

# 3 — ŞANS DAĞILIMI / CHANCE ALLOCATION — YENİ ÖNCELİKLİ FAZ

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

Hattrick match-engine dokümantasyonunda normal şansların midfield/possession ile dağıtıldığı ve regular chances'ın takım sektörlerine dağıtıldığı belirtiliyor. citeturn124845search0turn124845search3

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

ve M8 sonucu üzerinden:

```text
OwnExclusive
OpponentExclusive
OpenChancePool
OwnOpenExpected
OpponentOpenExpected
OwnRegularChanceExpected
OpponentRegularChanceExpected
```

alanları taşınıyor.

Uygulama commitleri:

```text
dfb2a778  M8 discrete chance allocation katmanı
...        M8 chance model entegrasyonu
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
M9 OwnChanceShare
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

Pressing toplam potansiyel normal şansları azaltabilen bir mekanizma; special events'i doğrudan azaltan bir mekanizma olarak ele alınmamalıdır. citeturn124845search1

Counter Attack, midfield dezavantajı koşulunda başarısız rakip normal atağından ek atak üretme mekanizmasıdır ve taktik seviyesinde savunma + passing etkisi kullanılır; passing savunmadan iki kat daha önemlidir. Ayrıca CA %7 midfield penalty taşır. citeturn124845search2turn124845search4

AIM normal chance dağılımında wing → centre dönüşümü oluşturur; kaynaklarda dönüşümün yaklaşık %15–30 aralığında olabildiği ve seviyenin outfield Passing toplamıyla ilişkili olduğu belirtiliyor. citeturn124845search5

Long Shots normal middle/wing attack'ların bir kısmını shooter vs goalkeeper eventine dönüştürür; shooter kalitesi Scoring + Set Pieces, goalkeeper tarafında da Goalkeeping + Set Pieces üzerinden değerlendirilir. citeturn124845search2turn124845search8

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

Mevcut M7.2 bu yapının önemli parçalarını taşıyor. Ancak Pressing / CA / LS / PC gibi taktikler gerçek event ve chance üretimine tam bağlanmış değil. Bu yüzden M7.2 korunacak, M8 bağlantısı yeniden kurulacak.

---

# 5 — SPECIAL EVENT MOTORU

Hattrick match-engine dokümantasyonuna göre önce special event olup olmayacağı, sonra event kategorisi ve event tipi seçilir; aynı special event türünün tekrarlarında olasılık azalabilir. Bu mekanizma V5'te ayrı event katmanı olarak modellenmelidir. citeturn124845search0

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

Bu nedenle Specialty doğrudan takım ratingine yapıştırılmayacaktır.

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

# 7 — ARAŞTIRMA / UYGULAMA YOL HARİTASI

```text
PHASE A   CHPP Specialty → Player             ✅
PHASE B   M3 Specialty-aware Profile          ✅

PHASE C   M8 discrete chance allocation       ✅ temel
PHASE C2  M9 chance-volume migration          ✅ ilk bağlantı

PHASE D   Chance total historical calibration 🔜
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
PHASE O   Full Monte Carlo                     🔜
PHASE P   Offline regression / real matches   🔜
```

### M5/M6 specialty sırası

M5/M6 specialty-aware candidate hâlâ yapılacak, ancak maç çekirdeğinin foundation'ını daha fazla eski chance matematiği üzerine büyütmemek için specialty'nin **scoring effect** tarafı M8/M9 event çekirdeği tamamlandıktan sonra production'a bağlanacaktır.

---

# 8 — UYGULAMA PRENSİPLERİ

1. **Önce veri → sonra mekanizma → sonra calibration → en son production katsayısı.**
2. Kaynakta doğrulanmayan specialty/tactic etkileri keyfi `+rating` bonusu olarak yazılmayacak.
3. M8 chance üretiminin sahibi olacak; M9 sadece çözümleyecek.
4. Taktiklerin chance conversion, chance suppression ve event üretimi ayrı tutulacak.
5. Special Event normal rating ile aynı şey olarak modellenmeyecek.
6. Monte Carlo en son genişletilecek; yanlış çekirdeği 1000 kere çalıştırmak kabul edilmeyecek.
7. M3-M6-M10-M11 formation/search mimarisi korunacak ve maç motorundan bağımsız olarak regresyonla güvence altında tutulacak.
8. Her önemli maç motoru değişikliği offline regression + gerçek maç guardrail ile kontrol edilecek.

### Kaynaklar

- Hattrick Wiki — Match engine
- Hattrick Wiki — Midfield
- Hattrick Wiki — Rules / Tactics
- Hattrick Wiki — Pressing
- Hattrick Wiki — Attack in the middle
- Hattrick Wiki — Long shots
- Hattrick CHPP XML documentation — Specialty
- 2026 academic Hattrick match-engine study — chance allocation / historical calibration reference
