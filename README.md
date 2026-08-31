# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

### GELİŞTİRME SIRASI — KAYBOLMAYACAK ANA YOL HARİTASI

Analiz butonuna basıldığında hedeflenen karar sırası:

`CHPP Veri → Rakip Analiz → Rakip Tehdit → Takım Kadro/11 Seçim → Bölgesel Rating → Oyuncu Rol/Davranış → Rakibe Karşı Rol Optimizasyonu → Final Taktik Optimizasyonu`

**Mevcut geliştirme aşaması: Motor 2 — Rakip Analiz + Kadro/11 Seçim Motoru.**

Ara motorlar geliştirilirken bu sıra korunacaktır. Bir motor test edilip doğrulanmadan sonraki motorun kararları canlı akışa zorlanmayacaktır.

### Motor 1 — CHPP Veri Motoru / Veri Katmanı ✅
CHPP'den kendi takımının oyuncu becerileri ve takım verileri ile rakibin son resmi maçına ait kadro, yıldız ve gerçek bölgesel rating verilerini toplar. Kupa ve hazırlık maçları otomatik olarak atlanır. Bu katman karar vermez; ham veriyi sağlar.

### Motor 2 — Rakip Analiz + Kadro/11 Seçim Motoru 🟡 AKTİF GELİŞTİRME
Rakibin son resmi maçını, gerçek 7 bölgesel ratingini, dizilişini, final saha 11'ini ve rakip profilini bizim kadro seçimimizden **önce** hazırlar. Motor 1 pozisyon uygunluğuyla ilk XI'yi üretir; Motor 2 daha sonra gerçek bölgesel ratingleri kullanarak yasal oyuncu takaslarını dener ve rakibe karşı daha iyi eşleşme sağlayan XI'yi kabul eder.

**RP karar girdisi değildir.** Rakip oyuncu RP tahmini yalnızca görüntüleme/yardımcı veri olarak kalır. Motor 2'nin oyuncu seçiminde gerçek oyuncu skill'leri + bölgesel rating + rakibin gerçek 7 ratingi kullanılır.

Motor 2'nin eşleşme skoru basit rating farkı yerine Hattrick maç motorunun yapısına daha yakın bir karşılaştırma kullanır:
- midfield için kübik chance-share yaklaşımı,
- merkezi hücum için %35,
- sol/sağ hücum için %25'er ağırlık,
- her hücumun karşı savunmayla eşleştirilmesi,
- rakibin beklenen hücum üretiminin de cezalandırılması.

Bu skor **maç sonucu tahmini değildir**; yalnızca iki yasal XI arasında seçim yapmak için kullanılır.

**Motor 2 kontrol noktaları:**
- CHPP rakip verisi Motor 2'ye geliyor mu? ✅
- Rakip dizilişi aktarılıyor mu? ✅
- Rakip 7 rating aktarılıyor mu? ✅
- Rakip son resmi maç final 11'i aktarılıyor mu? ✅
- Bizim 11 seçimi rakip profili oluşturulduktan sonra mı yapılıyor? ✅
- Motor 1 doğal pozisyon uygunluğu ilk XI'yi oluşturuyor mu? ✅
- Motor 2 gerçek bölgesel rating ile yasal takasları test ediyor mu? ✅
- RP oyuncu seçiminde kullanılıyor mu? ❌
- Hattrick tarzı midfield/chance ve attack-vs-opposite-defence eşleşmesi kullanılıyor mu? ✅
- Gerçek maç bağlamı ile aynı sonuç doğrulanıyor mu? 🟡 TEST AŞAMASI

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
Rakibin oyuncu skill'leri CHPP'den gelmediği için son resmi maçtaki yıldızlar, pozisyon/davranış ve gerçek takım ratingleri kullanılarak rakip oyuncu RP tahmini yapılır. Son testlerde RP değerlerinin 12–21 aralığına şişebildiği görüldü; bu değerler Motor 2'nin karar mekanizmasından çıkarılmıştır ve ayrı kalibrasyon konusu olarak tutulacaktır.

### Motor 7 — Rakip Tehdit Motoru 🟡 ALTYAPI HAZIR
Rakibin `ATT-L / ATT-C / ATT-R`, `MID` ve savunma ratinglerinden threat map üretir. Eşleşmeler:
- rakip sol hücum → bizim sağ savunma
- rakip merkez hücum → bizim merkez savunma
- rakip sağ hücum → bizim sol savunma

Bu motor rakip analizinden sonra çalışmalı; tek başına oyuncu davranışını değiştirmemelidir.

### Motor 8 — Final Taktik Optimizasyon Motoru ⏳
XI + pozisyon + individual behaviour + rakip threat + üç soruluk kullanıcı anketini tek final kararında birleştirir. Henüz canlıya alınmadı.

## Hedeflenen analiz bağlantı zinciri

```text
1 CHPP Veri Motoru
        ↓
2 Rakip Analiz Motoru
        ↓
7 Rakip Tehdit Motoru
        ↓
2 Kadro/11 Seçim kısmı
        ↓
3 Bölgesel Rating Motoru
        ↓
4 Oyuncu Rol/Davranış Motoru
        ↓
5 Rakibe Karşı Rol Optimizasyon Motoru
        ↓
3 Bölgesel Rating ile yeniden ölçüm
        ↓
8 Final Taktik Optimizasyon Motoru
```

Not: Rakip Rating Tahmin Motoru (6), rakip oyuncu RP tahmini gerektiğinde Rakip Analiz/Threat katmanına yardımcı veri sağlar; gerçek CHPP takım ratingi varsa öncelikli referans gerçek maç ratingidir.

## KULLANICI ANKETİ

V5 kullanıcıdan yalnızca üç bilgi alır:

1. Teknik direktör tarzı
2. Takım ruhu
3. Maç yaklaşımı: Normal / PIC / MOTS

CHPP'den alınabilen confidence gibi diğer psikoloji bilgileri kullanıcıya ayrıca sorulmaz; hesap için mevcutsa sistem içinden kullanılır.

## KOPYA AKIŞI

Analiz sonucunda kullanıcı takımını ve rakibi tek seferde kopyalayabilmelidir. Eski iki ayrı `KOPYALA` butonu kaldırılır; tek `KOPYALA` butonu şu iki bloğu arka arkaya üretir:

`HattrickAI V5 KOPYA` → bizim takım → diziliş → oyuncular → 7 bölgesel rating

`HattrickAI V5 KOPYA` → rakip → diziliş → oyuncular → 7 bölgesel rating

Bu metin doğrudan yeni Motor testlerinde sohbete yapıştırılabilecek formattadır.

## ÖNEMLİ REGRESSION TESTİ

S4MSUNFC vs Zeytinburnu Sahil Spor testi, Motor 2'nin ana regression senaryosudur.

S4MSUNFC son test:
`3-5-2`
`DEF 9.50 / 16.00 / 9.75`
`MID 7.50`
`ATT 9.50 / 11.50 / 10.00`

Zeytinburnu referansı:
`2-5-3`
`DEF 6.25 / 9.50 / 6.25`
`MID 7.00`
`ATT 10.00 / 13.00 / 8.50`

Son testte Motor 2 oyuncu yerleşimini değiştirdi ve kanat/merkez dağılımında küçük rating kazanımları üretti. Bu sonuç, motorun rakip verisini kullandığını gösteren olumlu bir sinyaldir; henüz tek başına doğrulama değildir.

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
