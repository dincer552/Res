# Res

## HattrickAI V5 motor mimarisi

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı motorlar halinde geliştirir.

## HEDEFLENEN ANA ANALİZ AKIŞI

Analiz butonuna basıldığında hedeflenen karar sırası aşağıdaki 11 aşamadır. Bu akış V5'in ana mimari referansıdır ve motorlar geliştirilirken sıra korunacaktır.

```text
CHPP / Veri
    ↓
1. Veri Hazırlama
    ├───────────────┐
    ↓               ↓
2. Rakip Analizi   3. Oyuncu Analizi
    └───────┬───────┘
            ↓
4. Aday Dizilişler
            ↓
5. Pozisyon Optimizasyonu
            ↓
6. Davranış Optimizasyonu
            ↓
7. Bölgesel Rating
            ↓
8. Maç Eşleşmesi
            ↓
9. Taktiksel Skor
            ↓
       daha iyi aday?
        ├─ EVET → 5
        └─ HAYIR
             ↓
10. En İyi Maç Planı
             ↓
11. Maç Tahmini
```

### Akışın temel prensibi

Bölgesel Rating Motoru karar veren tek başına bir motor değildir. Seçilen oyuncu + pozisyon + davranış kombinasyonunun yedi bölgesel ratingini üretir. Bu ratingler Maç Eşleşme Motoruna aktarılır; adayın rakibe karşı değeri burada ve Taktiksel Skor katmanında ölçülür.

Daha iyi bir aday bulunduğu sürece optimizasyon döngüsü tekrar Pozisyon Optimizasyonuna döner. Böylece sistem yalnızca tek oyuncuyu değil, **oyuncu + pozisyon + davranış + rakip eşleşmesi** bütününü optimize eder.

## GELİŞTİRME SIRASI

1. **CHPP Veri Motoru / Veri Katmanı** — CHPP'den ham takım, oyuncu, rakip ve maç verilerini sağlar. Karar vermez.
2. **Rakip Analiz Motoru** — Rakibin son resmi maçını, dizilişini, final 11'ini ve gerçek 7 bölgesel ratingini bizim kadro seçimimizden önce hazırlar.
3. **Oyuncu Analiz Motoru** — Kendi oyuncularımızın pozisyon uygunluklarını ve aday kullanım alanlarını çıkarır. XI seçmez.
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

### Motor 3 — Oyuncu Analiz Motoru 🟡 DÜZELTİLDİ / OFFLINE DOĞRULAMA BEKLİYOR
Motor 3'ün tek görevi kendi oyuncularını bağımsız biçimde analiz etmektir. Rakibi değerlendirmez, diziliş seçmez, XI oluşturmaz ve bölgesel rating üretmez. Her oyuncu için pozisyon uygunluk profilini üretir ve bunu Motor 4 ile Motor 5'e aktarır.

Oyuncu profili artık aşağıdaki temel bilgileri taşır:
- `IsEligible`
- `InjuryLevel`
- pozisyon adayları ve skorları
- birincil pozisyon
- ikincil pozisyon

`InjuryLevel == 999` olan oyuncular Motor 3 tarafından seçilebilir oyuncu adayı sayılmaz. Böylece sakat/oynanamaz oyuncunun daha sonraki pozisyon optimizasyonuna sızması engellenir.

Motor 3'ün güncel rolü:

`CHPP oyuncuları → oyuncu profilleri → pozisyon uygunluğu → Motor 4/5`

ÖNEMLİ: Pozisyon uygunluk skoru Hattrick maç ratingi değildir. Yalnızca oyuncunun belirli bir slot için göreli uygunluğunu ifade eden optimizasyon girdisidir.

### Motor 4 — Aday Diziliş Motoru 🟡 İSKELET HAZIR
3-5-2, 3-4-3, 4-4-2, 4-5-1, 2-5-3 ve 5-3-2 gibi yasal adayları üretir. Rakibe karşı nihai taktik değerini hesaplamaz; bu sorumluluk sonraki motorlardadır.

### Motor 5 — Pozisyon Optimizasyon Motoru 🟡 İSKELET HAZIR / MOTOR 3 ENTEGRASYON TESTİ
Motor 4'ten gelen diziliş adayını Motor 3'ün oyuncu profilleriyle eşleştirir. Aynı oyuncu aynı aday XI içinde iki kez kullanılamaz. Çıkış, oyuncu → slot atamalarından oluşan sıralı adaylardır.

Motor 5'te dikkat edilen ayrım:
- uygunluk skoru = optimizasyon girdisi
- bölgesel maç ratingi = Motor 7'nin çıktısı
- rakibe karşı taktik skor = Motor 8/9'un çıktısı

Bir sonraki offline doğrulama Motor 3 → Motor 4 → Motor 5 zincirinin tamamını gerçek CHPP verisiyle kontrol edecektir.

### Motor 6 — Davranış Optimizasyon Motoru 🟡 İSKELET
Oyuncunun normal / ofansif / defansif / ortaya doğru / kanada doğru davranışlarını aday olarak üretir. Nihai seçim henüz Motor 9/10'a bağlanmamıştır.

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

## OFFLINE TEST VERİSİ

Ana regression dosyası `HattrickAI_V5_CHPP_FullOffline_2026-09-01T08-49-54-690Z.json` olarak tutulur. Şema `hattrickai-v5-offline-test-v2`dir. Dosya CHPP kaynaklıdır ve credential, OAuth token veya session cookie içermez. fileciteturn879file0L11-L21

Bu veri seti S4MSUNFC → Zeytinburnu Sahil Spor maçı için kullanılacak. Dosya; kendi takım oyuncuları, training, maç geçmişi, rakip maçları, rakibin son resmi maç lineup'ı ve matchdetails gibi ham CHPP verilerini içerir. fileciteturn879file0L51-L55

Rakibin son resmi maçının 7 ratingi matchdetails içinden doğrulanabilir: DEF-L 6.25, DEF-C 9.50, DEF-R 6.25, MID 7.00, ATT-L 10.00, ATT-C 13.00, ATT-R 8.50. fileciteturn885file1L9-L10

Offline testlerde önce Motor 1 → Motor 2 → Motor 3, ardından Motor 4 → Motor 5 sıralı olarak doğrulanır. Bir üst motora geçilmeden önce giriş/çıkış sözleşmesi kontrol edilir.

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

Mevcut export analizinde S4MSUNFC 3-5-2, Zeytinburnu 2-5-3 olarak görünür ve rakip tehdit profili merkez 13.00, sol 10.00, sağ 8.50, orta saha baskısı 7.00 şeklindedir. fileciteturn886file0L53-L126

Mevcut S4MSUNFC yerleşiminde Enzo Bultot GK, Abeiku Takyi DEF-CL, Dawid Nocoń DEF-C ve Cristian Pesalovo DEF-CR olarak yer alır. fileciteturn888file0L27-L77

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
