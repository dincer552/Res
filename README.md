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
M6-A Global XI + behaviour search      🟡 ACTIVE / FORMATION SEARCH DÜZELTİLECEK
 ↓
DB1  Candidate Database #1             🟡 ACTIVE / FORMATION QUOTA GÜÇLENDİRİLECEK
 ↓
M7   Regional Rating Scenario           ✅ INTEGRATED
 ↓
M7.2 Advanced Tactical Scenario         ✅ INTEGRATED
 ↓
M8   Chance / Matchup                   ✅ INTEGRATED
 ↓
M9   Match Prediction                   🔴 OUTPUT TUTARLILIĞI DÜZELTİLECEK
 ↓
M10  Candidate Review / Search Gate     🟡 ACTIVE / GERÇEK FORMATION COMPETITION GEREKİYOR
 ↓
M6-B İkinci search loop                 🟡 ACTIVE / FORMATION BAŞINA SEARCH GÜÇLENDİRİLECEK
 ↓
DB2  Candidate Database #2              🟡 ACTIVE / FORMATION QUOTA GÜÇLENDİRİLECEK
 ↓
M11  Final Decision                     🟡 ACTIVE / M9 SONRASI DOĞRULAMA GEREKİYOR
 ↓
WEB  Final XI + Individual Order        ✅ CONNECTED
```

## 2026-09-02 MOTOR ÇIKTISI — İLK GERÇEK TEST SONUCU

Sonuç JSON çıktısı üzerinden yapılan incelemede anti-lock mekanizmasının **kısmen çalıştığı**, ancak henüz gerçek anlamda formasyon rekabeti oluşturmadığı görüldü.

### Gözlenen akış

```text
M4  → 6 legal formation
M5  → 120 XI candidate (~20 / formation)
M6-A → 44,372 evaluation
DB1 → 100 candidate
M10 → 35 candidate review
M6-B → 8,585 evaluation
DB2 → 100 candidate
M11 → final 2-5-3
```

DB1 ve DB2 dağılımı:

```text
2-5-3   30
3-4-3    1
3-5-2    1
4-4-2    1
4-5-1    1
5-3-2    1
```

Bu sonuç bize önemli bir ayrım gösteriyor:

> **Anti-lock şu anda formasyonların tamamen elenmesini engelliyor; fakat formasyonları eşit veya yeterli derinlikte yarıştırmıyor.**

Yani `30 / 1 / 1 / 1 / 1 / 1` dağılımı teknik olarak diversity kontrolünü geçebilir, fakat "hangi formasyon gerçekten daha iyi?" sorusunu güvenilir biçimde cevaplamak için yeterli değildir.

### M10 formasyon karşılaştırmasının mevcut durumu

İlk testte M10 altı formasyonu gördü; ancak alternatif formasyonların her birinde yalnızca tek aday vardı. Bu nedenle M10'un yaptığı karşılaştırma:

```text
2-5-3 → 30 aday arasından en iyi aday
Diğer 5 formasyon → 1 aday
```

şeklindeydi.

Bu nedenle 2-5-3'ün birinci çıkması **yanlış olmak zorunda değildir**, fakat henüz "global olarak en iyi formasyon" kanıtı değildir. Önce formasyon başına yeterli search derinliği oluşturulmalıdır.

## YENİ ANA HEDEF — GERÇEK FORMATION COMPETITION / ANTI-LOCK

V5 bundan sonra **2-5-3'e özel avantaj veya ceza vermeyecektir.** Bunun yerine her legal formasyon kendi search bütçesi içinde yeterli sayıda güçlü XI + Individual Order adayı üretmeli, bu adaylar rakibe karşı M7 → M7.2 → M8 → M9 hattında karşılaştırılmalı ve final karar bu gerçek yarıştan çıkmalıdır.

Buradaki hedef yalnızca DB'nin son aşamada her formasyondan bir kayıt tutması değildir. Asıl hedef:

```text
Her legal formation
      ↓
kendi search budget'ı
      ↓
kendi güçlü aday havuzu
      ↓
M7 → M7.2 → M8 → M9
      ↓
aynı kriterlerle karşılaştırma
      ↓
M10 formation competition
      ↓
M6-B targeted refinement + exploration
      ↓
DB2
      ↓
M11 final comparison
```

### Anti-lock prensipleri

1. **Her legal formasyon yarışta kalmalı.**
2. Her formasyon yalnızca 1 "koruma adayı" ile temsil edilmemeli; yeterli search derinliği olmalı.
3. M5 formasyon başına yaklaşık 20 XI üretmeye devam etmeli.
4. M6-A global beam search, güçlü bir formasyonun diğer formasyonları erken boğmasına izin vermemeli.
5. M6-A mümkünse **formation-aware / per-formation beam budget** kullanmalı.
6. DB1 her formasyon için yeterli sayıda güçlü adayı korumalı.
7. M10 DB1'i gerçek çoklu aday ve formasyon karşılaştırması olarak incelemeli.
8. M10 sonucu M6-B'ye search feedback vermeli.
9. M6-B hem lider çevresinde refinement hem de geride kalan formasyonlarda exploration yapmalı.
10. DB2'de aynı formasyon çeşitliliği ve aday derinliği korunmalı.
11. M11 tüm finalistleri aynı composite kriterlerle karşılaştırmalı.
12. Final sonuç yanında **formasyon bazlı karşılaştırma tablosu** gösterilmeli.

## KRİTİK DÜZELTME #1 — M9 PROBABILITY / EXPECTED GOALS TUTARLILIĞI

İlk gerçek motor çıktısında M9 için önemli bir tutarsızlık görüldü.

Örneğin çıktıdaki yaklaşık değerler:

```text
Expected Home Goals ≈ 1.006
Expected Away Goals ≈ 1.984

Win Probability  ≈ 1.23%
Draw Probability ≈ 8.34%
Loss Probability ≈ 90.43%
```

Expected goals değerleri bu yöndeyken Win/Draw/Loss dağılımının bu kadar ters yönde olması matematiksel olarak şüphelidir. Basit Poisson kontrolünde yaklaşık olarak:

```text
Home win  ≈ 18.6%
Draw      ≈ 21.3%
Away win  ≈ 60.1%
```

seviyesinde bir dağılım beklenir.

Bu nedenle **M9'un home/away semantiği, expected-goals üretimi ve Win/Draw/Loss hesaplaması birlikte incelenmeden final formasyon yarışmasına güvenilmemelidir.**

### M9 düzeltme kriterleri

```text
1. ExpectedHomeGoals / ExpectedAwayGoals anlamını doğrula
2. WinProbability hangi takımın kazanma olasılığı netleştir
3. DrawProbability doğrula
4. LossProbability hangi takımın kaybetme olasılığı netleştir
5. Win + Draw + Loss = 1 kontrolü ekle
6. Expected goals → probability yönünün tutarlı olduğunu test et
7. Home/Away swap test ekle
8. M9 çıktısını M8 StructuralChanceIndex ile karıştırmadığını doğrula
```

M9 düzeltilmeden M10/M11'in formasyon sıralamasını nihai kalite göstergesi olarak kabul etmeyeceğiz.

## KRİTİK DÜZELTME #2 — M6-A GERÇEK FORMATION SEARCH BÜTÇESİ

Mevcut ilk testte M6-A 44,372 aday değerlendirmiş olsa da DB1'e:

```text
2-5-3 → 30
diğer formasyonlar → 1'er
```

taşınmıştır.

Bu, global beam search'ün belirli bir formasyonda çok daha fazla arama derinliği oluşturduğunu ve diğer formasyonların DB'ye gelmeden önce aşırı budandığını gösteriyor.

### Tercih edilen çözüm

Sadece DB'nin sonradan `MaxPerFormation` ile trim edilmesi yeterli değildir. Çünkü sorun DB'ye gelmeden önce oluşmaktadır.

Öncelikli mimari seçenek:

```text
M5
 ↓
Formation A ── kendi beam/search budget
Formation B ── kendi beam/search budget
Formation C ── kendi beam/search budget
Formation D ── kendi beam/search budget
Formation E ── kendi beam/search budget
Formation F ── kendi beam/search budget
 ↓
M7 → M7.2 → M8 → M9
 ↓
merge
 ↓
DB1
```

Böylece bir formasyonun global beam'de erken öne çıkması diğer formasyonların search edilmesini engellemez.

### İlk hedef değerler

İlk iterasyonda kesin sayı kod incelemesinden sonra belirlenecek; ancak tasarım hedefi:

```text
Legal formation sayısı : 6
M5 aday/formasyon      : ~20
DB1 minimum/formasyon  : anlamlı bir çoklu aday sayısı
M10 aday/formasyon     : birden fazla güçlü aday
M6-B aday/formasyon    : refinement + exploration
DB2 minimum/formasyon  : anlamlı bir çoklu aday sayısı
```

**Önemli:** `6 × 20` değerini otomatik olarak DB1'e zorlamak doğru çözüm değildir. M6'nın search stratejisi, M5'ten gelen çeşitliliği gerçekten değerlendirecek şekilde tasarlanmalıdır.

## M10 — GERÇEK FORMATION COMPETITION

M10'un görevi artık sadece DB1 içinden sıralama yapmak değildir.

M10 şunları üretmelidir:

- formasyon başına aday sayısı,
- formasyon başına en iyi aday,
- formasyon başına en iyi tactical score,
- formasyon başına Win/Draw/Loss,
- composite score,
- lider ile ikinci arasındaki fark,
- formasyonun search depth'i,
- hangi formasyonların yeterli aday sayısına ulaşamadığı,
- M6-B için exploration/refinement feedback.

Örnek hedef çıktı:

```text
FORMATION COMPETITION

#1  2-5-3   12 candidates   Best Composite X.XXX
#2  3-5-2   12 candidates   Best Composite X.XXX
#3  3-4-3   12 candidates   Best Composite X.XXX
#4  4-5-1   12 candidates   Best Composite X.XXX
#5  4-4-2   12 candidates   Best Composite X.XXX
#6  5-3-2   12 candidates   Best Composite X.XXX

WINNER: 2-5-3
MARGIN vs #2: +X.XXX
```

Sayılar örnektir. Gerçek minimum değer kod ve performans testlerinden sonra belirlenecektir.

M10 ayrıca şu ayrımı açıkça göstermelidir:

```text
SEARCH-DEPTH OK
```

veya

```text
SEARCH-DEPTH INSUFFICIENT
```

Böylece tek adayla temsil edilen bir formasyonun haksız biçimde "kaybettiği" düşünülmez.

## M6-B — İKİNCİ SEARCH LOOP

M6-B artık yalnızca DB1 liderlerini tekrar değerlendiren bir motor olmamalıdır.

M10 feedback'iyle iki paralel amaç yürütülmelidir:

```text
M6-B
 ├─ REFINEMENT
 │    └─ güçlü adayların çevresinde daha iyi order / assignment ara
 │
 └─ EXPLORATION
      └─ DB1'de az temsil edilen formasyonları yeniden ara
```

Özellikle şu durum otomatik tetiklenmelidir:

```text
formation candidate count < minimum
        ↓
formation exploration budget artır
```

M6-B'nin başarı kriteri yalnızca daha yüksek score bulmak değildir. **Alternatif formasyonların gerçekten tekrar yarışa sokulmuş olması** da ölçülmelidir.

## DB1 / DB2 — CANDIDATE DATABASE KURALI

DB diversity mekanizması gerekli ama tek başına yeterli değildir.

DB'nin görevi:

- güçlü adayları korumak,
- formasyonların tamamen kaybolmasını önlemek,
- aynı formasyonun DB'yi tamamen doldurmasını engellemek,
- M10/M11 için yeterli karşılaştırma havuzu oluşturmak.

Ancak:

> **DB diversity, search diversity'nin yerine geçmez.**

Eğer M6 yalnızca bir formasyondan aday üretirse DB'nin sonradan diğer formasyonlardan birer kayıt ayırması gerçek rekabet yaratmaz.

## M11 — FINAL DECISION

M11 DB2 finalistlerini aynı standartta karşılaştırır.

Final sonuç:

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
- search depth / candidate count

ile birlikte görünmelidir.

M11 şu durumda final seçim yapmamalıdır:

```text
M9 probability consistency = FAIL
```

veya

```text
required formation search depth = INSUFFICIENT
```

Bu durumda sonuç "candidate winner" olabilir ancak **confidence / validation warning** ile gösterilmelidir.

## FORMATION COMPETITION GÖRÜNÜR SONUÇ

M11 sonunda aşağıdaki tipte bir tablo hedeflenmektedir:

```text
FORMATION COMPETITION

FORMATION  CANDIDATES  BEST WIN  DRAW  LOSS  COMPOSITE  SEARCH
2-5-3      12          XX.X%     XX.X% XX.X% X.XXX      OK
3-5-2      12          XX.X%     XX.X% XX.X% X.XXX      OK
3-4-3      12          XX.X%     XX.X% XX.X% X.XXX      OK
4-5-1      12          XX.X%     XX.X% XX.X% X.XXX      OK
4-4-2      12          XX.X%     XX.X% XX.X% X.XXX      OK
5-3-2      12          XX.X%     XX.X% XX.X% X.XXX      OK

WINNER: 2-5-3
MARGIN vs #2: +X.XXX
```

Buradaki değerler örnektir; gerçek değerler motor çıktısından gelecektir.

## UYGULAMA SIRASI — AŞAMA AŞAMA

### FAZ 1 — M9 doğruluk kilidi

```text
1. M9 home/away semantiğini incele
2. Expected Goals ile Win/Draw/Loss bağlantısını düzelt
3. Probability sum = 1 testi
4. Home/Away swap testi
5. Poisson sanity test
6. Offline regression fixture oluştur
```

**Çıkış kriteri:** M9 olasılıkları expected-goals yönüyle tutarlı ve testlerle korunuyor.

### FAZ 2 — M6-A formation-aware search

```text
1. M6-A'nın beam pruning noktasını tespit et
2. Formation başına search budget oluştur
3. Her legal formation için beam/search çalıştır
4. Global merge aşamasında adayları birleştir
5. DB1'e girmeden önce formation distribution logla
```

**Çıkış kriteri:** Hiçbir legal formasyon yalnızca sonradan eklenen "koruma kaydı" seviyesinde kalmıyor.

### FAZ 3 — DB1 gerçek çoklu aday havuzu

```text
1. Formation başına minimum candidate depth tanımla
2. DB1 trim/capacity mekanizmasını bu kuralla uyumlu hale getir
3. Formation count + best candidate + search depth logla
4. M10'a yeterli sayıda aday aktar
```

**Çıkış kriteri:** M10'da 30/1/1/1/1/1 gibi sahte diversity yerine gerçek çoklu aday dağılımı oluşuyor.

### FAZ 4 — M10 Formation Competition

```text
1. Formation bazlı leaderboard
2. Best candidate
3. Candidate count
4. Composite score
5. Win/Draw/Loss
6. Margin vs next formation
7. Search-depth warning
8. M6-B feedback
```

**Çıkış kriteri:** M10 "2-5-3 lider" demenin yanında diğer formasyonların ne kadar ve hangi veriyle geride kaldığını gösterebiliyor.

### FAZ 5 — M6-B refinement + exploration

```text
1. DB1 liderlerinde refinement
2. Az temsil edilen formasyonlarda exploration
3. Preserved player/order seed kullan
4. Formation başına sonuç dağılımını ölç
5. DB2'ye gerçek alternatif adayları taşı
```

**Çıkış kriteri:** M6-B yalnızca mevcut lideri cilalamıyor; geride kalan legal formasyonları da yeniden arıyor.

### FAZ 6 — DB2 + M11 final competition

```text
1. DB2 formation diversity
2. Formation leaderboard
3. M9 consistency gate
4. Search-depth gate
5. Final composite ranking
6. Winner + margin
7. Validation warning
```

**Çıkış kriteri:** M11 sonucu hem kazananı hem de kazananın hangi şartlarda seçildiğini kanıtlayabiliyor.

### FAZ 7 — Offline / Web doğrulama

```text
1. M9 regression
2. Formation anti-lock test
3. Search-depth test
4. M10 formation competition test
5. M6-B exploration test
6. DB2 diversity test
7. End-to-end M3 → M11 test
8. Web final output doğrulaması
```

## SEARCH LOCK TESTİ

Her analizde minimum olarak şu kontroller yapılmalıdır:

```text
Formasyon sayısı: 6

M5  → her formasyon candidate üretmiş mi?
M6-A → her formasyon yeterli search depth'e ulaşmış mı?
DB1 → her formasyon minimum aday sayısını koruyor mu?
M10 → her formasyon gerçek karşılaştırma için yeterli mi?
M6-B → eksik formasyonlar yeniden aranmış mı?
DB2 → her formasyon minimum aday sayısını koruyor mu?
M11 → final karşılaştırması tam mı?
```

Bir formasyon M5'te üretildiği halde daha sonraki aşamada yetersiz kalıyorsa sistem bunu sessizce kabul etmemeli; **hangi aşamada ve neden** kaybettiğini loglamalıdır.

## BAŞARI KRİTERLERİ

V5 formation competition tamamlanmış sayılmadan önce:

- [ ] M9 probability / expected-goals tutarlılığı doğrulandı
- [ ] M9 home/away swap testi geçiyor
- [ ] Her legal formation için gerçek search budget var
- [ ] M6-A'da formation starvation yok
- [ ] DB1 yalnızca diversity görüntüsü üretmiyor
- [ ] M10'da formasyon başına birden fazla güçlü aday var
- [ ] M10 search-depth durumunu raporluyor
- [ ] M6-B refinement + exploration yapıyor
- [ ] DB2 gerçek alternatif adayları koruyor
- [ ] M11 formation leaderboard üretiyor
- [ ] Final winner + margin gösteriliyor
- [ ] Offline regression testleri geçiyor
- [ ] Web çıktısı motorun tüm kritik sonuçlarını gösteriyor

## TASARIM KURALI

> **Motor bir formasyonu sevdiği için seçmeyecek. Formasyonlar gerçek search bütçeleriyle yarışacak; aynı standartta ölçülecek; en iyi doğrulanmış aday kazanacak.**

2-5-3 tekrar birinci çıkabilir. Önemli olan bunun artık bir varsayım değil, **diğer legal formasyonlarla yeterli search derinliğinde ölçülmüş ve M9 tutarlılığı doğrulanmış bir sonuç** olmasıdır.
