# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-02

Aktif branch: `v5`

V5 canlı analizde M3 → M11 çoklu aday zincirine sahiptir. Yeni hedef, motorun belirli bir formasyona erken kilitlenmesini engellemek ve tüm legal formasyonları rakibe karşı gerçek aday sonuçlarıyla yarıştırmaktır.

### Güncel motor durumu

```text
M3   Oyuncu Analizi                    ✅ LOCK / LIVE
 ↓
M4   Formasyon adayları                ✅ LIVE
 ↓
M5   XI / pozisyon adayları            ✅ geniş XI havuzu
 ↓
M6-A Global XI + behaviour search      🟡 ACTIVE
 ↓
DB1  Candidate Database #1             🟡 ACTIVE
 ↓
M7   Regional Rating Scenario           ✅ INTEGRATED
 ↓
M7.2 Advanced Tactical Scenario         ✅ INTEGRATED
 ↓
M8   Chance / Matchup                   ✅ INTEGRATED
 ↓
M9   Match Prediction                   ✅ INTEGRATED / CALIBRATION
 ↓
M10  Candidate Review / Search Gate     🟡 ACTIVE / ANTI-LOCK GELİŞTİRİLECEK
 ↓
M6-B İkinci search loop                 🟡 ACTIVE / ANTI-LOCK GELİŞTİRİLECEK
 ↓
DB2  Candidate Database #2              🟡 ACTIVE
 ↓
M11  Final Decision                     🟡 ACTIVE / FORMATION COMPETITION GELİŞTİRİLECEK
 ↓
WEB  Final XI + Individual Order        ✅ CONNECTED
```

## YENİ ANA HEDEF — FORMATION COMPETITION / ANTI-LOCK

Gerçek maç testinde 2-5-3 güçlü ve mantıklı bir sonuç üretse de, yalnızca final sonuca bakarak bunun gerçekten global optimum olduğunu söylemek mümkün değildir. M4 yapısal skorunda başka formasyonlar daha yüksek olabilirken downstream arama sonunda 2-5-3 öne çıkabilmektedir. Bu iyi bir sonuç olabilir; ancak alternatiflerin gerçekten aynı şartlarda yarıştığını kanıtlamamız gerekir.

Bu nedenle V5 bundan sonra **2-5-3'e özel avantaj veya ceza vermeyecektir.** Bunun yerine her legal formasyon kendi en iyi XI + Individual Order kombinasyonunu üretmeli, bu adaylar rakibe karşı M7 → M7.2 → M8 → M9 hattında karşılaştırılmalı ve final karar bu gerçek yarıştan çıkmalıdır.

### Anti-lock prensipleri

1. **Her legal formasyon yarışta kalmalı.**
2. Her formasyon için en az bir güçlü adayın M10/M11 finalist havuzuna taşınması hedeflenmeli.
3. M5 formasyon başına yaklaşık 20 XI üretmeye devam etmeli.
4. M6-A yalnızca tek bir `BestCandidate` bulma motoru gibi davranmamalı; formasyon çeşitliliğini koruyan Candidate DB üretmeli.
5. M10, DB1'i gerçek bir çoklu aday karşılaştırması olarak incelemeli.
6. M10 sonucu M6-B'ye search feedback vermeli.
7. M6-B, M6-A'nın lider formasyonunu körlemesine tekrar etmek yerine alternatif formasyonları özellikle yeniden denemeli.
8. DB2'de formasyon çeşitliliği korunmalı.
9. M11 tüm finalistleri aynı composite kriterlerle karşılaştırmalı.
10. Final sonuç yanında **formasyon bazlı karşılaştırma tablosu** gösterilmeli.

### Formasyon bazlı görünür sonuç

M11 sonunda aşağıdaki tipte bir tablo üretilecek:

```text
FORMATION COMPETITION

#1  2-5-3   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX
#2  3-5-2   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX
#3  4-5-1   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX
#4  3-4-3   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX
#5  4-4-2   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX
#6  5-3-2   Best XI   Win XX.X%   Draw XX.X%   Loss XX.X%   Composite X.XXX

WINNER: 2-5-3
MARGIN vs #2: +X.XXX
```

Buradaki değerler örnektir; gerçek değerler motor çıktısından gelecektir. Amaç kullanıcının sadece "motor 2-5-3 seçti" sonucunu değil, **2-5-3'ün diğer formasyonları neden ve ne kadar farkla geçtiğini** görebilmesidir.

### Search lock testi

Her analizde şu kontrol yapılmalıdır:

```text
Formasyon sayısı: 6
Her formasyonun DB1 adayı: >= 1
Her formasyonun DB2 adayı: >= 1
Her formasyonun finalist adayı: >= 1
```

Bir formasyon M5'te üretildiği halde M10/M11'e ulaşamıyorsa sistem bunu sessizce kabul etmemeli; log'da hangi aşamada elendiğini göstermelidir.

## MEVCUT MOTORLAR

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

M3 çıktısından doldurulabilir legal formasyon adayları üretir. Şu an altı legal formasyon değerlendirilir: 3-5-2, 3-4-3, 4-4-2, 4-5-1, 2-5-3 ve 5-3-2.

M4'ün görevi yapısaldır; rakibe karşı nihai karar vermemelidir. Rakip-aware yarış M6 ve downstream motorlarda yapılır.

### M5 — Position / XI Candidate Generator

Her legal formasyon için güçlü oyuncu-slot eşleşmelerinden yaklaşık 20 XI adayı üretir. Amaç tüm Cartesian uzayı körlemesine üretmek değil; yeterli çeşitliliği koruyan exact assignment + beam yaklaşımıdır.

### M6-A — Global Search

```text
M5 ~120 XI
 ↓
XI + Individual Order varyasyonları
 ↓
M7 → M7.2 → M8 → M9
 ↓
Candidate Database #1
 ↓
formation-diverse TOP 100
```

M6-A'nın temel çıktısı tek bir kazanan değil, downstream olarak güçlü ve çeşitlendirilmiş bir aday havuzudur.

### M10 — Candidate Review / Search Gate

M10 DB1 içindeki adayları karşılaştırır. Yeni mimaride M10:

- formasyon bazında lider adayları görmeli,
- güçlü/zayıf bölgeleri çıkarmalı,
- adaylar arasındaki farkı hesaplamalı,
- search feedback üretmeli,
- tek bir formasyona erken kilitlenmeyi engellemelidir.

### M6-B — Anti-Lock Second Search

M10 feedback'iyle ikinci arama turu yapılır.

```text
DB1
 ↓
M10 formation review
 ↓
strong regions / weak regions / candidate gaps
 ↓
M6-B
 ├─ lider aday çevresinde refinement
 ├─ zayıf kalan formasyonlarda exploration
 └─ özellikle DB1'de geride kalan legal formasyonları yeniden dene
 ↓
DB2
```

M6-B'nin başarı kriteri yalnızca daha yüksek score bulmak değildir. **Alternatif formasyonların gerçekten tekrar yarışa sokulmuş olması** da ölçülmelidir.

### M11 — Final Decision

M11 DB2 finalistlerini aynı standartta karşılaştırır. Final sonuç:

- Formation
- XI
- Individual Orders
- M7 regional ratings
- M7.2 tactical scenario
- M8 matchup/chance
- M9 Win/Draw/Loss
- tactical score
- structural score
- robustness
- formation competition rank

ile birlikte görünür olmalıdır.

## UYGULAMA SIRASI

```text
1. M5 geniş havuzu koru                         ✅
2. DB1 formation diversity                      🔧
3. M10 gerçek multi-candidate review             🔧
4. Formation competition tablosu                 🔧
5. M6-B anti-lock exploration                    🔧
6. DB2 formation diversity                       🔧
7. M11 formasyon bazlı final karşılaştırması     🔧
8. Web'de finalist/alternatif sonuçlarını göster 🔜
9. Offline regression ile tüm formasyonları doğrula 🔜
```

## Tasarım kuralı

> **Motor bir formasyonu sevdiği için seçmeyecek. Formasyonlar yarışacak; en iyi aday kazanacak.**

2-5-3 tekrar birinci çıkabilir. Önemli olan bunun artık bir varsayım değil, **diğer legal formasyonlarla ölçülmüş bir sonuç** olmasıdır.
