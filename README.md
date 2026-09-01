# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

### GELİŞTİRME SIRASI — KAYBOLMAYACAK ANA YOL HARİTASI

Analiz butonuna basıldığında hedeflenen karar sırası:

`CHPP Veri → Rakip Analiz → Rakip Tehdit → Takım Kadro/11 Seçim → Bölgesel Rating → Oyuncu Rol/Davranış → Rakibe Karşı Rol Optimizasyonu → Final Taktik Optimizasyonu`

**Mevcut geliştirme aşaması: Rakip Tehdit Motoru.**

Rakip Analiz Motoru ilk doğrulama aşamasını geçti ve gerçek rakip 7 ratingini kullanarak XI seçim akışına veri sağlıyor. Şimdi bu verinin bağımsız tehdit/fırsat haritasını sağlamlaştırıyoruz.

### Motor 1 — CHPP Veri Motoru / Veri Katmanı ✅
CHPP'den kendi takımının oyuncu becerileri ve takım verileri ile rakibin son resmi maçına ait kadro, yıldız ve gerçek bölgesel rating verilerini toplar. Kupa ve hazırlık maçları otomatik olarak atlanır. Bu katman karar vermez; ham veriyi sağlar.

### Motor 2 — Rakip Analiz + Kadro/11 Seçim Motoru 🟡 DOĞRULAMA / BAKIM
Rakibin son resmi maçını, gerçek 7 bölgesel ratingini, dizilişini, final saha 11'ini ve rakip profilini bizim kadro seçimimizden **önce** hazırlar. Pozisyon uygunluğuyla ilk XI oluşturulur; ardından gerçek bölgesel ratingler ile yasal oyuncu takasları denenir.

**RP karar girdisi değildir.** Rakip oyuncu RP tahmini yalnızca görüntüleme/yardımcı veri olarak kalır.

Eşleşme skoru:
- midfield için kübik chance-share yaklaşımı,
- merkezi hücum %35,
- sol/sağ hücum %25'er,
- her hücumun karşı savunmayla eşleştirilmesi,
- rakibin beklenen hücum üretiminin cezalandırılması.

Bu skor maç sonucu tahmini değildir; yasal XI adaylarını karşılaştırmak içindir.

### Motor 3 — Bölgesel Rating Motoru ✅
`RegionalRatingEngineFixed`, doğrulanmış rating katmanıdır. Pozisyon + skill + behaviour + maç bağlamından yedi bölgesel rating üretir.

S4MSUNFC — 3-5-2 regression referansı:
`DEF-L 10.25 / DEF-C 16.50 / DEF-R 10.25 / MID 7.25 / ATT-L 10.50 / ATT-C 12.00 / ATT-R 9.50`

Yeni doğrulanmış maç verisi gelmeden temel katsayılar değiştirilmemelidir.

### Motor 4 — Oyuncu Rol/Davranış Motoru 🟡
Oyuncunun normal / ofansif / defansif / ortaya doğru / kanada doğru seçeneklerinin rating hesabına etkisini üretir. Legal davranış adayları hazırlanmıştır; nihai otomatik seçim henüz Final Taktik katmanına bağlanmamıştır.

### Motor 5 — Rakibe Karşı Rol Optimizasyon Motoru ⏳
Rakip tehdit haritasını kullanarak oyuncu davranışlarını ve yönlerini rakibe karşı optimize eder. Bu motor, davranış değişikliğinin DEF/MID/ATT ratinglerine etkisini Bölgesel Rating Motoru üzerinden ölçmelidir.

### Motor 6 — Rakip Rating Tahmin Motoru 🟡 KALİBRASYON KONTROLÜ
Rakibin oyuncu skill'leri CHPP'den gelmediği için son resmi maçtaki yıldızlar, pozisyon/davranış ve gerçek takım ratingleri kullanılarak rakip oyuncu RP tahmini yapılır. Son testlerde RP değerlerinin 12–21 aralığına şişebildiği görüldü; bu değerler karar mekanizmasından çıkarılmıştır ve ayrı kalibrasyon konusu olarak tutulacaktır.

### Motor 7 — Rakip Tehdit Motoru 🟡 AKTİF GELİŞTİRME
Rakibin gerçek 7 bölgesel ratinginden iki yönlü eşleşme haritası üretir:
- rakip sol hücum → bizim sağ savunma tehdidi,
- rakip merkez hücum → bizim merkez savunma tehdidi,
- rakip sağ hücum → bizim sol savunma tehdidi,
- bizim sol hücum → rakibin sağ savunma fırsatı,
- bizim merkez hücum → rakibin merkez savunma fırsatı,
- bizim sağ hücum → rakibin sol savunma fırsatı.

Bu katman oyuncu seçmez ve RP üretmez. Sonraki Kadro/11 ve Rol Optimizasyon katmanlarının kullanacağı temiz eşleşme verisini sağlar.

### Motor 8 — Final Taktik Optimizasyon Motoru ⏳
XI + pozisyon + individual behaviour + rakip threat + üç soruluk kullanıcı anketini tek final kararında birleştirir. Henüz canlıya alınmadı.

## Hedeflenen analiz bağlantı zinciri

```text
CHPP Veri Motoru
        ↓
Rakip Analiz Motoru
        ↓
Rakip Tehdit Motoru   ← ŞİMDİ BURADAYIZ
        ↓
Takım Kadro/11 Seçim
        ↓
Bölgesel Rating
        ↓
Oyuncu Rol/Davranış
        ↓
Rakibe Karşı Rol Optimizasyonu
        ↓
Bölgesel Rating ile yeniden ölçüm
        ↓
Final Taktik Optimizasyonu
```

## KULLANICI ANKETİ

V5 kullanıcıdan yalnızca üç bilgi alır:

1. Teknik direktör tarzı
2. Takım ruhu
3. Maç yaklaşımı: Normal / PIC / MOTS

CHPP'den alınabilen confidence gibi diğer psikoloji bilgileri kullanıcıya ayrıca sorulmaz; hesap için mevcutsa sistem içinden kullanılır.

## KOPYA AKIŞI

Tek `İKİ TAKIMI KOPYALA` butonu bizim takım ve rakibi arka arkaya üretir:

`HattrickAI V5 KOPYA` → bizim takım → diziliş → oyuncular → 7 bölgesel rating

`HattrickAI V5 KOPYA` → rakip → diziliş → oyuncular → 7 bölgesel rating

## ÖNEMLİ REGRESSION TESTİ

S4MSUNFC vs Zeytinburnu Sahil Spor ana regression senaryosudur.

S4MSUNFC:
`3-5-2`
`DEF 9.50 / 16.00 / 9.75`
`MID 7.50`
`ATT 9.50 / 11.50 / 10.00`

Zeytinburnu Sahil Spor:
`2-5-3`
`DEF 6.25 / 9.50 / 6.25`
`MID 7.00`
`ATT 10.00 / 13.00 / 8.50`

Bu veri seti hem Rakip Analiz hem Rakip Tehdit Motoru için temel regression testidir.

## GELİŞTİRME KURALI

Her motor için:
1. Kod kontrolü
2. Girdi/çıktı sözleşmesi kontrolü
3. Bir sonraki motora veri aktarımı testi
4. Gerçek Hattrick referansıyla test
5. Hata analizi
6. Commit/deploy
7. Sonuç doğrulanmadan sonraki motora geçmeme

Bu belge motorlar geliştirilirken ana referans yol haritasıdır.
