# HattrickAI V5 — Motor Akışı ve Regional Rating Roadmap

## Nihai hedef

Analiz butonu sonunda yalnızca "en iyi 11" üretmemeli. Hedef:

**Rakibe göre en uygun oyuncular + pozisyonlar + bireysel davranışlar + bölgesel rating dengesi + maç planı.**

Hattrick'te bireysel emirler oyuncunun aynı pozisyonda kalıp katkılarını değiştirir; repositioning ise pozisyonu değiştirir. Bu iki kavram motorlarda ayrı tutulmalıdır. urlHattrick bireysel emir kurallarıhttps://wiki.hattrick.org/wiki/Rules

## Kritik mimari kararı

`RegionalRatingEngine` bir sıralı "motor aşaması" değildir. O, diğer motorların adaylarını tekrar tekrar ölçen **ortak hesaplama motorudur**.

Aynı şekilde rakip tehdit haritası, rakip ratinglerinden türeyen bir analiz parçasıdır; ayrı bir karar aşaması olarak tekrar tekrar kopyalanmamalıdır.

## Gerçek motor sırası

### Motor 1 — CHPP Veri Motoru

**Girdi:** CHPP takım, oyuncu, maç, lineup ve matchdetails verileri.

**Çıktı:**
- bizim oyuncu havuzu
- yaklaşan resmi maç
- rakip takım
- rakibin son tamamlanmış resmi lineup'ı
- rakibin gerçek 7 bölgesel ratingi
- maç bağlamı

Ham veriyi toplar; taktik karar vermez.

### Motor 2 — Rakip Analiz Motoru

**Girdi:** Motor 1'in rakip verileri.

**Çıktı:**
- rakip dizilişi
- rakibin DEF-L / DEF-C / DEF-R
- rakibin MID
- rakibin ATT-L / ATT-C / ATT-R
- rakip hücum tehdidi → bizim savunma eşleşmeleri
- bizim hücum fırsatı → rakibin savunma eşleşmeleri
- rakibin güçlü/zayıf sektörleri

Sektör eşleşmeleri Hattrick'in attack-vs-opposite-defence mantığına göre kurulmalıdır: sağ hücum rakibin sol savunmasıyla, sol hücum rakibin sağ savunmasıyla eşleşir. urlHattrick hücum sektörlerihttps://wiki.hattrick.org/wiki/Attack

### Motor 3 — Bölgesel Rating Motoru

Bu motor **sıralı bir aşama değildir**.

Her aday lineup ve her bireysel davranış kombinasyonunda çağrılır.

**Girdi:**
- oyuncular
- pozisyonlar
- bireysel emirler
- maç bağlamı

**Çıktı:** 7 gerçek takım ratingi:
- DEF-L
- DEF-C
- DEF-R
- MID
- ATT-L
- ATT-C
- ATT-R

RP bu hesaplamanın karar girdisi değildir.

### Motor 4 — Oyuncu Rol ve Davranış Motoru

**Girdi:** aday oyuncu + pozisyon.

**Çıktı:** o pozisyon için yasal bireysel emirler:
- Normal
- Ofansif
- Defansif
- Ortaya doğru
- Kanada doğru

Her aday Motor 3'e gönderilir ve **takımın 7 ratinginde ne değiştirdiği ölçülür**.

Bu motor henüz rakip adına doğrudan karar vermemelidir; önce doğru davranış adaylarını ve rating etkilerini üretmelidir.

### Motor 5 — Maç Eşleşme Motoru

Rakip Analiz Motorunun ürettiği sektör tehdit/fırsatları ile Motor 3'ün rating sonuçlarını karşılaştırır.

Örnek:

`Biz ATT-R → Rakip DEF-L`

`Rakip ATT-L → Biz DEF-R`

`Biz MID → Rakip MID`

Bu katman bir oyuncuyu tek başına puanlamaz; **takımın maç eşleşmesini** puanlar.

### Motor 6 — Kadro ve Davranış Optimizasyon Motoru

Asıl optimizasyon burada yapılır.

Aday kombinasyon:

`oyuncu + pozisyon + bireysel emir`

→ Motor 3 rating
→ Motor 5 maç eşleşmesi
→ toplam maç planı skoru

şeklinde değerlendirilir.

Böylece oyuncu seçimi ile davranış seçimi birbirinden kopmaz.

Motor 2'nin rakip bilgisini kullanır fakat RP'ye göre seçim yapmaz.

### Motor 7 — Maç Planı Motoru

Son seçilen XI ve davranışlar üzerinden:
- nihai 7 rating
- midfield/possession beklentisi
- hücum-savunma sektör eşleşmeleri
- seçilen takım taktiği
- maç bağlamı

birleştirilir.

Bu motor kullanıcıya sunulacak nihai önerinin kaynağıdır.

### Motor 8 — Maç Sonucu / Şans Modeli

Ratingler güvenilir hale geldikten sonra:
- midfield → şans sahipliği
- ATT-C vs DEF-C
- ATT-R vs DEF-L
- ATT-L vs DEF-R
- duran toplar
- taktik kaynaklı şans değişimleri
- ileride özel olaylar

hesaplanır.

## Analiz butonu için kesin veri akışı

```text
CHPP Veri Motoru (1)
        ↓
Rakip Analiz Motoru (2)
        ↓
Aday XI / pozisyon havuzu
        ↓
Oyuncu Rol ve Davranış Motoru (4)
        ↓
Bölgesel Rating Motoru (3)
        ↓
Maç Eşleşme Motoru (5)
        ↓
Kadro ve Davranış Optimizasyon Motoru (6)
        ↺ yeni aday → tekrar 3 + 5
        ↓
Maç Planı Motoru (7)
        ↓
Şans Modeli (8)
```

## Mevcut kodda tespit edilen mimari sorunlar

1. `AnalysisService` şu anda rakip profilinden sonra doğrudan XI üretip ardından rating hesaplıyor. Bu, rating motorunun XI seçiminde yeterince erken kullanılmasını engelliyor.
2. `BehaviourEngine` ve `BehaviourRatingService` mevcut fakat `AnalysisService` akışına henüz tam bağlı değil.
3. `BehaviourOptimizer` bireysel davranışı seçebiliyor fakat seçilen davranışın XI optimizasyonuna geri beslenmesi henüz tamamlanmış değil.
4. `RegionalRatingEngine` ortak hesaplayıcı olarak kullanılmalı; ayrı bir tek-seferlik aşama gibi düşünülmemeli.
5. Rakip tehdit haritası `OpponentThreatEngine` ile oluşturuluyor; bu bilgi Rakip Analiz Motorunun çıktısı olarak kabul edilmeli, ayrı ve bağımsız bir karar motoru olarak çoğaltılmamalı.
6. Mevcut `AnalysisService` içinde kendi dizilişi `3-5-2` sabitlenmiş durumda. Nihai mimaride diziliş de aday planın bir değişkeni olmalı; ancak bunu gerçek rating doğrulaması tamamlanmadan aktif optimizasyona açmayacağız.
7. Anket/maç bağlamı yalnızca en son rating düzeltmesi olarak uygulanmamalı. Kararı etkileyen bir bağlamsa optimizasyon değerlendirmesine girmeli.

## Uygulama sırası

Önce **Motor 4 — Oyuncu Rol ve Davranış Motoru** tamamlanacak.

Sonra Motor 3 ile gerçek rating etkisi doğrulanacak.

Ardından Motor 5 ve Motor 6 birlikte bağlanacak.

En son Motor 7 nihai maç planını üretecek.

## Test kuralı

Her motor değişikliğinde:

1. build
2. gerçek S4MSUNFC test verisi
3. beklenen 7 rating ile karşılaştırma
4. motorun aldığı/gönderdiği veri kontrolü
5. ancak test geçerse sonraki motora geçiş

Tek bir ekran görüntüsüne göre katsayı değiştirilmez. Gerçek Hattrick maç raporlarından oluşan validation set korunur.

## V5 kuralı

Sadece `HattrickAI_V5` aktiftir. V1/V3/V4 kodu açıkça istenmedikçe kullanılmaz. `YEDEK` arşivdir.