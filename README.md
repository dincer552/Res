# HattrickAI V5

## Aktif geliştirme hattı

**Aktif branch: `v5`**

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı 11 aşamalı motor mimarisiyle geliştirir.

## Hedef analiz akışı

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

Döngünün amacı tek bir oyuncuyu değil; **oyuncu + pozisyon + davranış + rakip eşleşmesi** bütününü optimize etmektir.

## Motor isimleri ve mevcut durum

| Sıra | Gerçek işlev | Kod / sınıf | Durum |
|---|---|---|---|
| M1 | CHPP Veri Motoru / Veri Katmanı | CHPP veri katmanı | ✅ Çalışıyor |
| M2 | Rakip Analiz Motoru | `Motor2OpponentAwareRefiner` / rakip profil katmanı | 🟡 Bakım + doğrulama |
| M3 | Oyuncu Analiz Motoru | `PlayerAnalysisEngine` | 🟡 Kod hazır, offline doğrulama bekliyor |
| M4 | Aday Diziliş Motoru | `FormationCandidateEngine` | 🟡 İskelet hazır |
| M5 | Pozisyon Optimizasyon Motoru | `XIOptimizer` / XI optimizasyon katmanı | 🟡 İskelet + entegrasyon doğrulaması |
| M6 | Davranış Optimizasyon Motoru | `BehaviourOptimizer` | 🟡 İskelet |
| M7 | Bölgesel Rating Motoru | `RegionalRatingEngineFixed` | ✅ Referans motor |
| M8 | Maç Eşleşme Motoru | planlanan | ⏳ Sıradaki ana geliştirme |
| M9 | Taktiksel Skor Motoru | planlanan | ⏳ |
| M10 | Final Taktik Optimizasyon Motoru | planlanan | ⏳ |
| M11 | Maç Tahmin Motoru | planlanan | ⏳ |

### M1 — CHPP Veri Motoru / Veri Katmanı
Ham takım, oyuncu, rakip ve maç verilerini sağlar. Karar vermemelidir.

### M2 — Rakip Analiz Motoru
Rakibin bizim XI seçimimizden bağımsız olarak son resmi maçını, dizilişini, final 11'ini, gerçek 7 bölgesel ratingini ve tehdit profilini hazırlar. **RP karar girdisi değildir.**

### M3 — Oyuncu Analiz Motoru
Sadece kendi oyuncularının pozisyon uygunluk profillerini üretir. XI seçmez, diziliş seçmez, rakip skoru üretmez ve bölgesel rating üretmez. Oyuncu profilinde `IsEligible`, `InjuryLevel`, pozisyon adayları, birincil ve ikincil pozisyon bulunur. `InjuryLevel == 999` oyuncular aday değildir.

### M4 — Aday Diziliş Motoru
Yasal ve doldurulabilir diziliş adaylarını üretir. Mevcut adaylar arasında 3-5-2, 3-4-3, 4-4-2, 4-5-1, 2-5-3 ve 5-3-2 vardır. Nihai rakip/taktik skorunu hesaplamaz.

### M5 — Pozisyon Optimizasyon Motoru
Diziliş adayındaki slotlara oyuncu atamalarını takım seviyesinde optimize eder. Aynı oyuncu aynı aday XI içinde iki kez kullanılamaz. Uygunluk skoru gerçek Hattrick maç ratingi değildir; yalnızca optimizasyon girdisidir.

### M6 — Davranış Optimizasyon Motoru
Normal, ofansif, defansif, ortaya doğru ve kanada doğru davranış adaylarını değerlendirir. Nihai karar M9/M10 tarafında verilecektir.

### M7 — Bölgesel Rating Motoru
`RegionalRatingEngineFixed` yedi bölgesel rating üretir. Yeni doğrulanmış maç verisi olmadan temel katsayılar değiştirilmemelidir.

### M8 — Maç Eşleşme Motoru
Bizim hücum/savunma bölgelerimizi rakibin karşı savunma/hücum bölgeleriyle eşleştirecek. **Bir sonraki ana geliştirme aşaması.**

### M9 — Taktiksel Skor Motoru
Hücum avantajı, savunma güvenliği, orta saha ve risk/denge üzerinden aday planları karşılaştıracak.

### M10 — Final Taktik Optimizasyon Motoru
En iyi diziliş + ilk 11 + individual behaviour + rakip eşleşmesini final maç planına dönüştürecek.

### M11 — Maç Tahmin Motoru
Final plan üzerinden pozisyon şansı, gol olasılığı ve kazanma olasılığı üretecek.

## Offline regression testi

Kalıcı test girdisi:

`TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json`

Şema:

`hattrickai-v5-offline-test-v2`

Ana senaryo:

- **Biz:** S4MSUNFC (`1080139`)
- **Rakip:** Zeytinburnu Sahil Spor (`653953`)
- **Maç:** `769648177`
- **Tarih:** 2026-09-06 15:00 UTC
- **Saha:** Zeytinburnu Sahil Spor
- **Durum:** S4MSUNFC deplasman

Export CHPP kaynaklıdır ve credentials, OAuth token veya session cookie içermez.

### Ground-truth rakip ratingi

Zeytinburnu Sahil Spor'un referans resmi maçından alınan gerçek 7 bölgesel rating:

```text
DEF-L 6.25
DEF-C 9.50
DEF-R 6.25
MID   7.00
ATT-L 10.00
ATT-C 13.00
ATT-R 8.50
```

### Mevcut S4MSUNFC maç verisi

```text
Diziliş: 3-5-2
DEF-L 9.50
DEF-C 16.00
DEF-R 9.75
MID   7.50
ATT-L 9.50
ATT-C 11.50
ATT-R 10.00
```

Bu iki rating seti birbirine karıştırılmamalıdır: CHPP'den gelen gerçek rating **ground-truth**, motorun ürettiği rating ise **tahmin/aday çıktısıdır**.

## Offline test sonucu — 2026-09-01

Şu anki kayıt durumu:

- M1 veri seti: ✅ mevcut
- M2 rakip verisi: ✅ test girdisi mevcut / 🟡 motor doğrulaması sürüyor
- M3 kaynak kodu: ✅ incelendi / 🟡 tam offline PASS bekliyor
- M4 kaynak kodu: ✅ incelendi / 🟡 tam offline PASS bekliyor
- M5 kaynak kodu: 🟡 entegrasyon doğrulaması bekliyor
- M6: 🟡 iskelet
- M7: ✅ referans motor
- M8-M11: ⏳ geliştirme bekliyor

**Önemli:** Tam Motor 1 → Motor 5 regression sonucu henüz `PASS` olarak işaretlenmemiştir. Test dosyasının araç üzerinden tam içeriği tek seferde çalışma ortamına alınamadığı için çalıştırılmamış bir test başarılı kabul edilmemiştir.

## Mimari olarak doğrulanan noktalar

1. Motor 3 yalnızca oyuncu profili üretir.
2. Motor 4 yalnızca yasal/doldurulabilir diziliş adaylarını üretir.
3. Motor 3'ün pozisyon uygunluk skoru gerçek maç ratingi değildir.
4. Gerçek CHPP ratingi ile motor tahmini ayrı tutulur.
5. Rakip analizi kendi XI seçimimize bağımlı olmamalıdır.
6. Aynı oyuncu bir aday XI içinde iki slotta kullanılamaz.
7. Optimizasyon döngüsü yalnızca daha iyi aday bulunduğunda devam etmelidir.
8. Bir motorun sorumluluğu sonraki motorun işini gizlice üstlenmemelidir.

## Dikkat edilmesi gereken eski yol

`HattrickAI_V5/Core/XIOptimizer.cs` içinde eski oyuncu → pozisyon atama yolu hâlâ bulunmaktadır. Yeni 11 aşamalı mimaride bunun canlı çağrı zincirinde nerede kullanıldığı kesinleştirilmeden silinmemelidir. `PositionSuitabilityEngine` ise mevcut API'yi koruyan uyumluluk katmanı olarak Motor 3'e delegasyon yapmaktadır.

## Geliştirme kuralı

Her motor için sıra:

1. Kod kontrolü
2. Girdi/çıktı sözleşmesi kontrolü
3. Önceki motorla entegrasyon testi
4. Offline CHPP regression testi
5. Hata analizi
6. Düzeltme ve commit
7. Deploy
8. Deploy sonucu doğrulama
9. Sonuç PASS olmadan sonraki motorun nihai karar mantığına geçmeme

Bu README, V5 motorlarının mevcut durumunu ve hedef akışı kaybetmemek için ana teknik kayıt olarak tutulacaktır.
