# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

### Motor 0 — CHPP Veri Katmanı
CHPP'den kendi takımının oyuncu becerileri ve takım verileri ile rakibin son resmi maçına ait kadro, yıldız ve gerçek bölgesel rating verilerini toplar. Kupa ve hazırlık maçları otomatik olarak atlanır. Bu katman karar vermez; ham veriyi sağlar.

### Motor 1 — Position Suitability
`PositionSuitabilityEngine` her oyuncunun 11 hedef pozisyondaki uygunluk puanını üretir. Böylece tek bir genel oyuncu puanı yerine oyuncu-pozisyon ilişkisi ölçülür.

### Motor 2 — XI Optimizer
`XIOptimizer` ve `XIOptimizationService`, Motor 1'in uygunluk skorlarını kullanarak aynı anda 11 oyuncuyu ve pozisyonlarını seçmek için oluşturuldu. Amaç, oyuncuları sırayla greedy biçimde dağıtmak yerine takım bütünlüğünü dikkate alan bir XI üretmektir.

### Motor 3 — Behaviour Engine (sıradaki katman)
Seçilen pozisyon için oyuncunun Normal, Ofansif, Defansif, Ortaya Doğru veya Kanada Doğru davranış alternatiflerini değerlendirecek. Behaviour seçimi yalnızca görüntü bilgisi değildir; Regional Rating Engine'e gerçek bir girdi olacaktır.

### Motor 4 — Regional Rating Engine
`RegionalRatingEngineFixed`, pozisyon + skill + behaviour + maç bağlamından DEF-L/DEF-C/DEF-R, MID ve ATT-L/ATT-C/ATT-R ratinglerini üretir. Mevcut V5'in doğrulanmış rating baseline'ı bu katmanda korunur.

### Motor 5 — Opponent Rating Estimator
Rakibin oyuncu skill'leri CHPP üzerinden alınamadığından `OpponentRatingEstimator`, son resmi maçtaki yıldız, pozisyon/davranış ve takımın gerçek bölgesel ratinglerini kullanarak rakip oyuncu RP tahmini üretir. Rakibin gerçek takım ratingi varsa doğrudan referans kabul edilir.

### Motor 6 — Opponent Threat Engine
Rakibin 7 bölgesel ratinginden hangi kanat, merkez veya savunma sektörünün tehdit oluşturduğunu çıkaracaktır.

### Motor 7 — Behaviour Optimizer
Rakip tehdidi ile kendi rating değişimini birlikte değerlendirerek her oyuncu için en mantıklı davranışı seçecektir.

### Motor 8 — Final Tactical Optimizer
Oyuncu seçimi + pozisyon + behaviour + rakip tehditleri + üç soruluk maç anketini tek final kararında birleştirecektir.

## Motor bağlantı zinciri

`CHPP Data → Position Suitability → XI Optimizer → Behaviour Engine → Regional Rating → Opponent Analysis → Behaviour Optimizer → Final Tactical Optimizer`

Her motorun tek bir sorumluluğu vardır. Bir sonraki motor, öncekinin ürettiği yapılandırılmış sonucu girdi olarak kullanır.

## Kullanıcı anketi

V5 kullanıcıdan yalnızca üç bilgi alır:

1. Teknik direktör tarzı
2. Takım ruhu
3. Maç yaklaşımı: Normal / PIC / MOTS

CHPP'den alınabilen diğer psikoloji bilgileri ayrıca kullanıcıya sorulmaz.

## Rating doğrulama referansı

S4MSUNFC — 3-5-2 için mevcut Hattrick referansı:

`DEF-L 10.25 / DEF-C 16.50 / DEF-R 10.25 / MID 7.25 / ATT-L 10.50 / ATT-C 12.00 / ATT-R 9.50`

Bu referans yeni rating değişikliklerini doğrulamak için regression hedefidir.
