# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5'nin hedefi yalnızca iyi bir ilk 11 bulmak değil; **oyuncu → formasyon → XI → rating → taktik → chance → goal → W/D/L → formasyon karşılaştırması → final** zincirini tek ve tutarlı bir maç motoru olarak çalıştırmaktır.

2026 Hattrick araştırma makalesi M8/M9 için ana araştırma referansıdır. Makalede desteklenen mekanizmalar production'a alınacak; CHPP historical veri ise doğrulama ve kalibrasyon için kullanılacaktır. Tek fixture veya küçük örneklem, makaledeki güçlü mekanizmanın yerine otomatik olarak geçirilmeyecektir.

# ANA V5 MOTOR MİMARİSİ

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
Poisson + Monte Carlo
   ↓
W / D / L
   ↓
M10 Formasyon Competition
   ↓
M6-B Exploration + Refinement
   ↓
DB2
   ↓
M11 Final Selector
   ↓
WEB
```

## MOTOR SORUMLULUKLARI

### M3 — Oyuncu Profili

Oyuncunun skill, position/order/side, experience, form/loyalty ve Specialty bilgisini sonraki motorlara taşır. Specialty düz rating bonusu değildir; event bağlamında kullanılmalıdır.

### M4 — Formasyon

Tüm legal formasyonları üretir. Hiçbir legal formasyon M6 öncesinde global ranking ile silinmez.

### M5 — XI Adayları

Her legal formasyon için geniş XI havuzu üretir. M6'nın gerçek arama alanı budur.

### M6 — Global Search

Formation-aware search kullanır. DB1/DB2 formation diversity korunur; güçlü tek formasyon diğerlerini beam'den silemez.

### M7 — Gerçek Takım Ratingleri

M3 oyuncu profillerini Left/Centre/Right Defence, Midfield ve Left/Centre/Right Attack ratinglerine dönüştürür. M8/M9 rating uydurmaz.

### M7.2 — PDF Taktik Mekanikleri

M7 ratinglerinden seçili taktiğin chance-distribution ve tactic context çıktısını üretir. Kağıttaki yedi taktik artık canonical enum içinde vardır:

```text
Normal
AiM
AoW
Counter Attack
Long Shots
Pressing
Play Creatively
```

Taktik seviyesi mevcut V5 ölçeğinde 0–10 proxy'dir. Makaledeki `RT` ölçeği ile birebir aynı olduğu kanıtlanmadan Appendix C regresyonları production coefficient olarak kullanılmaz.

### M8 — Chance Engine

M8'in canonical akışı:

```text
Eq.1  Midfield → possession
Eq.2  5 exclusive + 5 shared chance structure
Eq.3  L / C / R / DFK / IFK / PK distribution
Eq.4  attack vs defence sector scoring probability
AiM / AoW migration
CA opportunity
Pressing suppression
LS opportunity
```

PDF baseline değerleri production çekirdeğidir:

```text
Left   25.65%
Centre 36.15%
Right  25.65%
DFK     5.86%
IFK     4.18%
PK      2.51%
```

LMR toplamı `%87.45`; beklenen normal attack volume `10`; dolayısıyla expected LMR volume `8.745`. Mevcut 60 CHPP maçında gözlenen LMR ortalaması `8.80` olup baseline ile uyumludur.

Pressing'de, makaledeki one-team pressing açıklamasına göre iki takımın generated normal attack hacmi de suppression ile azaltılır. İki takımın aynı anda Pressing oynadığı özel ilişki için iki taraflı tactic input henüz ayrıca modellenmemiştir.

### M9 — Goal / Event Engine

M9 artık M8 fırsatlarını tek bir aggregated rating modeliyle geçiştirmek yerine event katmanı üzerinden toplam gole bağlamaya başlamıştır.

PDF Tables 4–5 tabanında şu event sınıfları kodlanmıştır:

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

Event feasibility current XI'daki specialty + position bilgisine göre belirlenir. Makaledeki player-based event mean `0.841`, team-based event mean `0.372` production event baseline olarak kullanılır.

Henüz bilinmeyen hidden inputs açıkça pending durumundadır: set-piece taker skill, bazı event-specific conversion detayları, rakip Specialty ayrıntıları, PNF/PDIM kesin conversion ve Long Shot scoring eğrisi.

### Monte Carlo

Mevcut Monte Carlo dosyası halen geçiş katmanıdır; final hedefi aggregated xG etrafında rastgele multiplier örneklemek değil, M8/M9 olaylarını ayrı ayrı örnekleyerek maç simülasyonu yapmaktır.

### M10 / M6-B / M11

Bu zincir formation-aware olarak hazırdır:

```text
M10 → tüm legal formation leaderboard
M6-B → DB1 seed + exploration/refinement
DB2 → formation depth
M11 → final karşılaştırma
```

M6-B'nin M10 rank-driven search budget kullanımı henüz ayrı bir sonraki iyileştirme olarak tutulmaktadır; mevcut sistemde formation diversity garanti edilmektedir.

## PDF MATCH ENGINE FAZLARI

```text
FAZ A  CHPP Specialty → Player                         ✅
FAZ B  M3 Specialty-aware profile                     ✅
FAZ C1 M8 discrete chance allocation                  ✅
FAZ C2 M9 chance-volume migration                     ✅
FAZ D  Historical chance-volume validation             ✅
FAZ E  PDF sector baseline + 60-match validation       ✅
FAZ F  AiM / AoW migration + M7.2 handoff             🔧 CI gate
FAZ G  Pressing suppression                            🔧 CI gate
FAZ H  Counter Attack opportunity engine               🔧 CI gate
FAZ I  Long Shots opportunity engine                   🔜 next
FAZ J  Play Creatively event-volume layer              🔧 partial / CI gate
FAZ K  Specialty event engine                          🔧 started
FAZ L  Specialty ↔ tactic / weather                    🔜
FAZ M  M9 event-based goal resolution                  🔧 started
FAZ N  Historical event calibration                     🔜
FAZ O  Full event-based Monte Carlo                    🔜
FAZ P  Offline regression + real-match validation       🔜
```

### FAZ geçiş kuralı

```text
Kod
 ↓
CI compile
 ↓
offline regression
 ↓
mechanism sanity
 ↓
PASS
 ↓
sonraki faz
```

Bir faz CI regression'da kırmızıysa sonraki faz production'a geçirilmez.

## PDF'DEN KULLANILACAK VERİ SINIFLARI

### Doğrudan production mekanizması

- Midfield → possession
- discrete chance structure
- L/C/R chance distribution
- set-piece event baseline
- attack vs defence sector probability
- AiM / AoW migration
- CA midfield penalty and missed-chance opportunity
- Pressing normal-chance suppression
- documented specialty/event relationships

### Hidden / eksik input nedeniyle kontrollü kullanılacaklar

- set-piece taker skill
- Long Shot scoring probability eğrisi için açık denklem eksikliği
- rakip Specialty event-support detayları
- PNF/PDIM bazı conversion detayları
- tactic `RT` ölçeğinin mevcut V5 0–10 level ile eşlemesi

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

60 maçlık dataset PDF baseline'a yakınlığı doğrulamıştır; production coefficient'lerini tek başına değiştirmemiştir.

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

## SONRAKİ UYGULAMA SIRASI

```text
1. CI/build temizle ve regression kırmızısını sıfırla
2. M7.2 → M8 tactic handoff'u tüm 7 taktikte doğrula
3. M8 chance volume + tactic effects'i stabilize et
4. M9 event → goal bağlantısını genişlet
5. LS + PNF/PDIM eksik event mekaniklerini açık kaynak verilerle tamamla
6. Specialty ↔ weather/tactic bağlantısını ekle
7. Historical event datasetini genişlet
8. Gerçek event-based Monte Carlo'ya geç
9. M10 formation leaderboard'ı M9/MC çıktısıyla besle
10. M6-B'yi formation rank/depth'e göre daha akıllı refine et
11. M11'de final risk-adjusted comparison yap
12. Offline + gerçek maç regression ile production gate koy
```
