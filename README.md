# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5'nin hedefi yalnızca iyi bir ilk 11 bulmak değil; **oyuncu → formasyon → XI → rating → taktik → chance → goal → W/D/L → formasyon karşılaştırması → final** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmaktır.

2026 Hattrick araştırma makalesi M8/M9 için ana araştırma referansıdır. Makalede desteklenen mekanizmalar production'a alınacak; CHPP historical veri ise doğrulama ve kalibrasyon için kullanılacaktır. Tek fixture veya küçük örneklem, makaledeki güçlü mekanizmanın yerine otomatik olarak geçirilmeyecektir.

# ANA V5 MOTOR MİMARİSİ

```text
M3
Oyuncu Profili
   │
   ├── Specialty
   ├── Passing / Defending / Scoring
   ├── Experience
   └── Position / Order / Side
   ↓
M4
Formasyon
   ↓
M5
XI Adayları
   ↓
M6
Global Search
   ↓
M7
GERÇEK Takım Ratingleri
   │
   ├── Left / Centre / Right Defence
   ├── Midfield
   └── Left / Centre / Right Attack
   ↓
M7.2
Taktik Mekanikleri
   │
   ├── Normal
   ├── AiM
   ├── AoW
   ├── Counter Attack
   ├── Long Shots
   ├── Pressing
   └── Play Creatively
   ↓
M8
CHANCE ENGINE
   │
   ├── Possession / ownership — PDF Eq.1
   ├── Chance volume — PDF Eq.2
   ├── Distribution — PDF Eq.3
   ├── Sector conversion — PDF Eq.4
   ├── AiM / AoW migration
   ├── Counter Attack
   ├── Pressing suppression
   ├── Long Shots
   └── Special-event opportunity generation
   ↓
M9
GOAL ENGINE
   │
   ├── Normal goals
   ├── Counter Attack goals
   ├── Long Shot goals
   ├── Set-piece goals
   ├── PNF / PDIM goals
   └── Specialty-event goals
   ↓
Poisson / Monte Carlo
   ↓
W / D / L
   ↓
M10
Formasyon Karşılaştırması
   ↓
M6-B
İkinci Arama / Exploration + Refinement
   ↓
DB2
   ↓
M11
Final Seçici
   ↓
WEB
Final XI + individual orders
```

## KATMANLARIN SORUMLULUKLARI

### M3 — Oyuncu Profili

M3 yalnızca oyuncu rating üretmez. Oyuncunun sonraki maç motorlarında kullanılacak bağlamsal profilini taşır:

```text
Skill profile
Position / Order / Side
Experience
Form / Loyalty
Specialty
   ├── Technical
   ├── Quick
   ├── Powerful
   ├── Unpredictable
   └── Head
```

Specialty düz rating bonusu değildir. M8/M9'da olay bağlamına göre kullanılacaktır.

### M4 — Formasyon

Tüm legal formasyonları üretir. Hiçbir formasyon M6'ya gelmeden elenmemelidir.

### M5 — XI Adayları

Her legal formasyon için yeterli sayıda XI adayı üretir. Oyuncu kombinasyonları M6'nın gerçek arama alanını oluşturur.

### M6 — Global Search

Formation-aware search uygular. Güçlü tek formasyonun diğer legal formasyonları global beam'den silmesini engeller.

DB1'e giden adaylar formasyon derinliğini korur.

### M7 — Gerçek Takım Ratingleri

M3 oyuncu profillerini Hattrick pozisyon katkılarıyla gerçek takım ratinglerine dönüştürür:

```text
Left Defence
Centre Defence
Right Defence
Midfield
Left Attack
Centre Attack
Right Attack
```

M7 maç motorunun rating kaynağıdır. M8/M9 rating uydurmaz; M7'nin ürettiği ratingleri kullanır.

### M7.2 — Taktik Mekanikleri

M7 ratingleri üzerine seçilen taktiğin maç motoru etkilerini ekler:

```text
AiM
AoW
Counter Attack
Long Shots
Pressing
Play Creatively
```

Burada tactic level, oyuncu becerileri, formation ve rakip bağlamı birlikte değerlendirilir. Taktik etkileri M8'e açık parametreler halinde aktarılır.

### M8 — Chance Engine

M8 artık maçın **şans üretim motorudur**. Önce sahiplik/chance volume, sonra dağılım, sonra sektör eşleşmesi çözülür.

#### PDF Eq.1 — Midfield → Possession

Midfield ratinglerden possession/chance ownership hesaplanır. Bu, eski basit `ownMidfieldShare` yaklaşımının production çekirdeğinin yerine geçmiştir.

#### PDF Eq.2 — Discrete chance structure

Araştırmadaki yapısal baseline:

```text
5 exclusive own
5 exclusive opponent
5 open/shared pool
```

Ayrıca araştırma modelinde beklenen normal attack volume 10'dur.

#### PDF Eq.3 — Chance distribution

```text
Left       25.65%
Centre     36.15%
Right      25.65%
DFK         5.86%
IFK         4.18%
PK          2.51%
```

LMR toplamı `%87.45` olduğundan 10 normal attack üzerinden beklenen regular sector volume `8.745` olur.

60 CHPP maçında gözlenen L/M/R ortalaması `8.80` olduğundan PDF baseline mevcut dataset ile uyumludur.

#### PDF Eq.4 — Sector attack vs defence

M8 sektörleri karşılıklı ratinglerle çözer:

```text
Own Left Attack    vs Opponent Right Defence
Own Centre Attack  vs Opponent Centre Defence
Own Right Attack   vs Opponent Left Defence
```

Bu eşleşmelerden sektör bazlı gol/başarı olasılığı elde edilir.

#### Tactic chance migration

```text
AiM       wing → centre
AoW       centre → wings
CA        missed normal chances → counter opportunities
LS        L/M/R → long-shot opportunities
Pressing  normal chance suppression
```

Taktik katsayıları yalnızca makalede desteklenen aralık/formüller üzerinden uygulanacaktır.

#### Special events

M8 specialty, tactic ve match context üzerinden special-event **fırsatlarını** üretir. Event'in gole dönüşmesi M9'un sorumluluğudur.

### M9 — Goal Engine

M9 M8'in ürettiği fırsatları gole çevirir.

```text
Normal sector goals
CA goals
LS goals
Set-piece goals
PNF / PDIM
Specialty-event goals
```

Sonrasında aynı expected-goals çiftinden:

```text
Poisson → score distribution → W/D/L
Monte Carlo → scenario robustness
```

üretilir.

M9 içinde ikinci bir rating veya chance allocation modeli oluşturulmayacaktır.

### M10 — Formasyon Karşılaştırması

M10 artık yalnızca tek finalist seçmez. Tüm legal formasyonların DB1 sonuçlarını karşılaştırır:

```text
Rank
Candidate count
Composite score
Win probability
Margin vs next
Search depth status
```

### M6-B — Exploration + Refinement

M10'dan gelen formation competition sonucunu kullanarak ikinci arama yapılır. DB1 adayları seed olarak korunur; diğer formasyonların exploration hakkı kaybolmaz.

### M11 — Final Seçici

DB2 içinden tüm legal formasyonları tekrar karşılaştırır ve final XI'ı seçer.

## PDF MATCH ENGINE — UYGULAMA SIRASI

```text
FAZ A  CHPP Specialty → Player                         ✅
FAZ B  M3 Specialty-aware profile                     ✅
FAZ C1 M8 discrete chance allocation                  ✅
FAZ C2 M9 chance-volume migration                     ✅
FAZ D  Historical chance-volume validation             ✅
FAZ E  PDF sector baseline + historical validation     🔄
FAZ F  AiM / AoW chance migration                      🔜
FAZ G  Pressing suppression                            🔜
FAZ H  Counter Attack opportunity engine               🔜
FAZ I  Long Shots opportunity engine                   🔜
FAZ J  Play Creatively special-event engine             🔜
FAZ K  Specialty event engine                          🔜
FAZ L  Specialty ↔ tactic / weather                     🔜
FAZ M  M9 event-based goal resolution                  🔜
FAZ N  Historical event calibration                     🔜
FAZ O  Full Monte Carlo                                🔜
FAZ P  Offline regression + real-match validation       🔜
```

## PDF'DEN KULLANILACAK VERİ SINIFLARI

### Doğrudan production mekanizması

- Midfield → possession
- discrete chance structure
- L/C/R chance distribution
- set-piece event baseline
- attack vs defence sector probability
- AiM / AoW migration
- CA midfield penalty and opportunity mechanism
- Pressing normal-chance suppression
- Long Shots opportunity conversion
- specialty event relationships where inputs are available

### Hidden / eksik input nedeniyle kontrollü kullanılacaklar

- set-piece taker skill
- bazı specialty hidden-event modifiers
- bazı event-specific conversion parameters

Bu alanlarda CHPP'den olmayan skill veya parametre uydurulmayacaktır.

## KALİBRASYON KURALI

```text
PDF mechanism
      ↓
production baseline
      +
CHPP historical matches
      ↓
error / residual analysis
      ↓
regression / confidence test
      ↓
only if justified → production adjustment
```

60 maçlık mevcut dataset production baseline'ı değiştirmek için değil, PDF mekanizmasının bizim lig/verimizde nasıl davrandığını doğrulamak için kullanılmaktadır.

## FORMATION ENGINE KURALI

```text
M4 legal formations
        ↓
M5 candidates per formation
        ↓
M6-A formation-aware search
        ↓
DB1: min formation depth
        ↓
M10 formation leaderboard
        ↓
M6-B exploration/refinement
        ↓
DB2
        ↓
M11 final comparison
```

Maç motoru geliştirmeleri bu formation-search mimarisini bozmayacaktır.

## DURUM

Şu anda PDF tabanlı M8/M9 revizyonu üzerinde çalışılıyor. Sonraki uygulama sırası README'deki FAZ F → P zincirine göre ilerleyecek; her fazdan sonra offline regression çalıştırılacak ve başarısızsa bir sonraki faza geçilmeyecektir.
