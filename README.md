# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

### Motor 0 — CHPP Veri Katmanı
CHPP'den kendi takımının oyuncu becerileri ve takım verileri ile rakibin son resmi maçına ait kadro, yıldız ve gerçek bölgesel rating verilerini toplar. Kupa ve hazırlık maçları otomatik olarak atlanır. Bu katman karar vermez; ham veriyi sağlar.

### Motor 1 — Position Suitability ✅
`PositionSuitabilityEngine` her oyuncunun desteklenen hedef pozisyonlardaki temel uygunluk puanını üretir. Bu bir placement heuristic'tir; Hattrick maç rating formülü değildir.

### Motor 2 — XI Optimizer ✅
`XIOptimizer` ve `XIOptimizationService`, Motor 1 skorlarını kullanarak tüm mevcut oyuncu havuzunda 11 oyuncu ile pozisyonlar arasında bir atama çözer. Eski 20 oyuncu sınırına dayalı DP kaldırıldı; tam kadro için dikdörtgen Hungarian assignment kullanılıyor. Canlı `AnalysisService` artık eski greedy `BuildOwnLineup` yerine bu motoru çağırıyor.

### Motor 3 — Behaviour Engine ✅
`BehaviourEngine`, seçilen pozisyon için Hattrick'in izin verdiği individual order seçeneklerini üretir. `BehaviourRatingService`, aynı oyuncu/pozisyon için her legal order'ı `RegionalRatingEngineFixed` üzerinden yeniden hesaplayarak behaviour'un gerçekten DEF/MID/ATT ratinglerine girdi olmasını sağlar.

Hattrick'in CHPP dokümantasyonunda `Behaviour` 0=Normal, 1=Offensive, 2=Defensive, 3=Towards Middle, 4=Towards Wing ve 5-7 extra-role dönüşümleri olarak tanımlanır. PositionCode, behaviour sonrası sahada oynanan pozisyonu temsil eder. citeturn283397search0turn283397search1

### Motor 4 — Behaviour Optimizer ✅ (hazır, kontrollü)
`BehaviourOptimizer`, Motor 3'ün legal order adaylarını rakibin ratinginden çıkarılan tehdit haritasıyla değerlendirir. Şimdilik nihai otomatik davranışı canlı XI'ye zorlamaz; mevcut doğrulanmış rating baseline'ını bozmadan ayrı bir karar katmanı olarak durur.

### Motor 5 — Regional Rating Engine ✅
`RegionalRatingEngineFixed`, tek rating otoritesidir. Pozisyon + skill + behaviour + maç bağlamından yedi bölgesel rating üretir. Hattrick contribution tablosundaki position/order katkıları bu katmanın temelidir. citeturn283397search2

### Motor 6 — Opponent Rating Estimator ✅
Rakibin oyuncu skill'leri CHPP'den gelmediği için `OpponentRatingEstimator`, son resmi maçtaki yıldızlar, pozisyon/davranış ve gerçek takım ratinglerini kullanarak rakip oyuncu RP tahmini yapar. Rakibin gerçek maç ratingi mevcutsa takım ratingi doğrudan referanstır.

### Motor 7 — Opponent Threat Engine ✅
`OpponentThreatEngine`, rakibin `ATT-L / ATT-C / ATT-R`, `MID` ve savunma ratinglerinden sade bir threat map üretir. Karşılık eşleşmesi: rakip sol hücum → bizim sağ savunma, rakip merkez hücum → bizim merkez savunma, rakip sağ hücum → bizim sol savunma.

### Motor 8 — Final Tactical Optimizer ⏳
Bu son katman henüz canlıya alınmadı. Hedefi; XI + pozisyon + individual behaviour + rakip threat + üç soruluk kullanıcı anketini tek bir final kararında birleştirmek.

## Motor bağlantı zinciri

```text
CHPP Data
   ↓
Position Suitability (M1)
   ↓
XI Optimizer (M2)
   ↓
Behaviour Engine (M3)
   ↘
    Regional Rating (M5)
   ↗
Behaviour Optimizer (M4) ← Opponent Threat (M7) ← Opponent Rating (M6)
   ↓
Final Tactical Optimizer (M8)
```

`AnalysisService` şu anda M0 → M1 → M2 → M5 akışını canlıda kullanıyor. M3/M4/M6/M7 bağlantı sınıfları hazır; M8 tamamlandığında bunlar tek karar döngüsüne alınacak.

## Kullanıcı anketi

V5 kullanıcıdan yalnızca üç bilgi alır:

1. Teknik direktör tarzı
2. Takım ruhu
3. Maç yaklaşımı: Normal / PIC / MOTS

CHPP'den alınabilen confidence gibi diğer psikoloji bilgileri kullanıcıya ayrıca sorulmaz.

## Rating doğrulama referansı

S4MSUNFC — 3-5-2 için mevcut Hattrick referansı:

`DEF-L 10.25 / DEF-C 16.50 / DEF-R 10.25 / MID 7.25 / ATT-L 10.50 / ATT-C 12.00 / ATT-R 9.50`

Bu referans yeni rating değişikliklerini doğrulamak için regression hedefidir. Yeni doğrulanmış maç verisi gelmeden temel katsayılar değiştirilmemelidir.
