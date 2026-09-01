# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

## HEDEFLENEN ANA ANALİZ AKIŞI

Analiz butonuna basıldığında hedeflenen karar sırası aşağıdaki 11 aşamadır. Bu akış V5'in ana mimari referansıdır ve motorlar geliştirilirken sıra korunacaktır.

```text
                    ┌─────────────────────┐
                    │     CHPP / Veri     │
                    │  Takım + Oyuncular  │
                    │  Rakip + Maç bilgisi│
                    └──────────┬──────────┘
                               │
                               ▼
                 ┌──────────────────────────┐
                 │  1. VERİ HAZIRLAMA       │
                 │  Oyuncu skill / form     │
                 │  Rakip XI / skill        │
                 │  Maç koşulları           │
                 └────────────┬─────────────┘
                              │
                ┌─────────────┴─────────────┐
                ▼                           ▼
      ┌───────────────────┐       ┌───────────────────┐
      │ 2. RAKİP ANALİZİ  │       │ 3. OYUNCU ANALİZİ │
      │                   │       │                   │
      │ Rakip diziliş     │       │ Oyuncuların       │
      │ 7 bölgesel rating │       │ pozisyon uygunluğu│
      │ Tehdit bölgeleri  │       │                   │
      └─────────┬─────────┘       └─────────┬─────────┘
                │                           │
                └─────────────┬─────────────┘
                              ▼
                ┌──────────────────────────┐
                │ 4. ADAY DİZİLİŞLER       │
                │                          │
                │ 3-5-2 / 3-4-3 / 2-5-3   │
                │ ve diğer yasal dizilişler│
                └────────────┬─────────────┘
                             │
                             ▼
                ┌──────────────────────────┐
                │ 5. POZİSYON OPTİMİZERİ   │
                │                          │
                │ Oyuncu → Pozisyon        │
                │ yasal aday kombinasyonları│
                └────────────┬─────────────┘
                             │
                             ▼
                ┌──────────────────────────┐
                │ 6. DAVRANIŞ OPTİMİZERİ   │
                │                          │
                │ Normal / Ofansif         │
                │ Defansif / Ortaya        │
                │ Kanada                    │
                └────────────┬─────────────┘
                             │
                             ▼
              ┌────────────────────────────────┐
              │ 7. BÖLGESEL RATING MOTORU     │
              │                                │
              │ DEF-L / DEF-C / DEF-R         │
              │ MID                            │
              │ ATT-L / ATT-C / ATT-R         │
              │                                │
              │ Pozisyon + skill + davranış +  │
              │ maç bağlamından rating üretir │
              └───────────────┬────────────────┘
                              │
                              ▼
                ┌──────────────────────────┐
                │ 8. MAÇ EŞLEŞME MOTORU    │
                │                          │
                │ ATT-R ↔ Rakip DEF-L      │
                │ ATT-C ↔ Rakip DEF-C      │
                │ ATT-L ↔ Rakip DEF-R      │
                │                          │
                │ DEF-R ↔ Rakip ATT-L      │
                │ DEF-C ↔ Rakip ATT-C      │
                │ DEF-L ↔ Rakip ATT-R      │
                │ + Orta saha              │
                └────────────┬─────────────┘
                             │
                             ▼
                ┌──────────────────────────┐
                │ 9. TAKTİKSEL SKOR        │
                │                          │
                │ Hücum avantajı           │
                │ Savunma güvenliği        │
                │ Orta saha                │
                │ Risk / denge             │
                └────────────┬─────────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │ Daha iyi aday var│
                    │       mı?        │
                    └────────┬─────────┘
                        EVET │
                             └──────────────► 5. POZİSYON OPTİMİZERİ

                        HAYIR
                             │
                             ▼
                ┌──────────────────────────┐
                │ 10. EN İYİ MAÇ PLANI     │
                │                          │
                │ Diziliş                  │
                │ İlk 11                   │
                │ Oyuncu emirleri          │
                │ Bölgesel ratingler       │
                │ Rakibe karşı avantajlar  │
                └────────────┬─────────────┘
                             │
                             ▼
                ┌──────────────────────────┐
                │ 11. MAÇ TAHMİNİ          │
                │                          │
                │ Pozisyon şansı           │
                │ Gol olasılığı            │
                │ Kazanma olasılığı        │
                └──────────────────────────┘
```

### Akışın temel prensibi

Bölgesel Rating Motoru karar veren tek başına bir motor değildir. Seçilen oyuncu + pozisyon + davranış kombinasyonunun yedi bölgesel ratingini üretir. Bu ratingler Maç Eşleşme Motoruna aktarılır; adayın rakibe karşı değeri burada ve Taktiksel Skor katmanında ölçülür.

Daha iyi bir aday bulunduğu sürece optimizasyon döngüsü tekrar Pozisyon Optimizasyonuna döner. Böylece sistem yalnızca tek oyuncuyu değil, **oyuncu + pozisyon + davranış + rakip eşleşmesi** bütününü optimize eder.

## GELİŞTİRME SIRASI

1. **CHPP Veri Motoru / Veri Katmanı** — CHPP'den ham takım, oyuncu, rakip ve maç verilerini sağlar. Karar vermez.
2. **Rakip Analiz Motoru** — Rakibin son resmi maçını, dizilişini, final 11'ini ve gerçek 7 bölgesel ratingini bizim kadro seçimimizden önce hazırlar.
3. **Oyuncu Analiz Motoru** — Kendi oyuncularımızın yasal pozisyonlardaki uygunluklarını ve aday kullanım alanlarını çıkarır.
4. **Aday Diziliş Motoru** — Maç için değerlendirilebilecek yasal dizilişleri üretir.
5. **Pozisyon Optimizasyon Motoru** — Oyuncu → pozisyon kombinasyonlarını takım seviyesinde değerlendirir.
6. **Davranış Optimizasyon Motoru** — Oyuncu emirlerinin rating üzerindeki etkisini adaylar halinde üretir.
7. **Bölgesel Rating Motoru** — Pozisyon + skill + davranış + maç bağlamından yedi bölgesel rating üretir.
8. **Maç Eşleşme Motoru** — Bizim hücum/savunma bölgelerimizi rakibin karşı savunma/hücum bölgeleriyle eşleştirir ve orta saha etkisini değerlendirir.
9. **Taktiksel Skor Motoru** — Hücum avantajı, savunma güvenliği, orta saha ve risk/denge üzerinden aday planları karşılaştırır.
10. **Final Taktik Optimizasyon Motoru** — En iyi diziliş + 11 + oyuncu emirleri kombinasyonunu final maç planına dönüştürür.
11. **Maç Tahmin Motoru** — Final plan üzerinden pozisyon şansı, gol olasılığı ve kazanma olasılığı üretir.

> Not: Bu 11 aşamalı akış hedef mimaridir. Her motorun canlı karar zincirine bağlandığı varsayılmamalıdır; geliştirme durumu aşağıdaki bölümlerde ayrıca belirtilir.

## MOTOR DURUMLARI

### Motor 1 — CHPP Veri Motoru / Veri Katmanı ✅
CHPP'den kendi takımının oyuncu becerileri ve takım verileri ile rakibin son resmi maçına ait kadro, yıldız ve gerçek bölgesel rating verilerini toplar. Kupa ve hazırlık maçları otomatik olarak atlanır. Bu katman karar vermez; ham veriyi sağlar.

### Motor 2 — Rakip Analiz Motoru 🟡 DOĞRULAMA / BAKIM
Rakibin son resmi maçını, gerçek 7 bölgesel ratingini, dizilişini, final saha 11'ini ve rakip profilini bizim kadro seçimimizden **önce** hazırlar. Pozisyon uygunluğuyla ilk XI oluşturulur; ardından gerçek bölgesel ratingler ile yasal oyuncu takasları denenir.

**RP karar girdisi değildir.** Rakip oyuncu RP tahmini yalnızca görüntüleme/yardımcı veri olarak kalır.

Eşleşme skoru:
- midfield için kübik chance-share yaklaşımı,
- merkezi hücum %35,
- sol/sağ hücum %25'er,
- her hücumun karşı savunmayla eşleştirilmesi,
- rakibin beklenen hücum üretiminin cezalandırılması.

Bu skor maç sonucu tahmini değildir; yasal XI adaylarını karşılaştırmak içindir.

### Motor 3 — Oyuncu Analiz Motoru ⏳
Kendi oyuncularının pozisyon uygunluğunu ve takım içindeki kullanılabilir adaylarını çıkarır. Bu katman henüz final optimizasyon döngüsünün tamamına bağlanmış değildir.

### Motor 4 — Aday Diziliş Motoru ⏳
3-5-2, 3-4-3, 2-5-3 ve diğer yasal diziliş adaylarını üretmek için ayrıştırılmış bir katman olarak planlanmıştır.

### Motor 5 — Pozisyon Optimizasyon Motoru ⏳
Oyuncu → pozisyon eşleşmelerini takım seviyesinde değerlendirecek optimizasyon katmanıdır.

### Motor 6 — Davranış Optimizasyon Motoru 🟡
Oyuncunun normal / ofansif / defansif / ortaya doğru / kanada doğru seçeneklerinin rating hesabına etkisini üretir. Legal davranış adayları hazırlanmıştır; nihai otomatik seçim henüz Final Taktik katmanına bağlanmamıştır.

### Motor 7 — Bölgesel Rating Motoru ✅
`RegionalRatingEngineFixed`, doğrulanmış rating katmanıdır. Pozisyon + skill + behaviour + maç bağlamından yedi bölgesel rating üretir.

S4MSUNFC — 3-5-2 regression referansı:
`DEF-L 10.25 / DEF-C 16.50 / DEF-R 10.25 / MID 7.25 / ATT-L 10.50 / ATT-C 12.00 / ATT-R 9.50`

Yeni doğrulanmış maç verisi gelmeden temel katsayılar değiştirilmemelidir.

### Motor 8 — Maç Eşleşme Motoru ⏳
Bizim hücum ve savunma bölgelerini rakibin karşı bölgeleriyle eşleştirir. Tehdit/fırsat verisini aday taktiklerin değerlendirilmesinde kullanır.

### Motor 9 — Taktiksel Skor Motoru ⏳
Aday planları hücum avantajı, savunma güvenliği, orta saha ve risk/denge kriterleriyle karşılaştırır.

### Motor 10 — Final Taktik Optimizasyon Motoru ⏳
En iyi diziliş + ilk 11 + individual behaviour + rakip eşleşmesini tek final maç planında birleştirir.

### Motor 11 — Maç Tahmin Motoru ⏳
Final maç planından pozisyon şansı, gol olasılığı ve kazanma olasılığı üretir.

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

Bu veri seti hem Rakip Analiz hem Maç Eşleşme Motoru için temel regression testidir.

## GELİŞTİRME KURALI

Her motor için:
1. Kod kontrolü
2. Girdi/çıktı sözleşmesi kontrolü
3. Bir sonraki motora veri aktarımı testi
4. Gerçek Hattrick referansıyla test
5. Hata analizi
6. Commit/deploy
7. Sonuç doğrulanmadan sonraki motora geçmeme

Bu belge V5 motorları geliştirilirken ana referans yol haritasıdır. Aktif geliştirme hattı yalnızca **`v5`** branch'idir.
