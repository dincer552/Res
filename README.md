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
| M3 | Oyuncu Analiz Motoru | `PlayerAnalysisEngine` | ✅ Offline test PASS |
| M4 | Aday Diziliş Motoru | `FormationCandidateEngine` | ✅ Offline test PASS |
| M5 | Pozisyon Optimizasyon Motoru | `XIOptimizer` / XI optimizasyon katmanı | 🟠 Offline test PARTIAL — düzeltme gerekli |
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

### M3 — Oyuncu Analiz Motoru — PASS ✅

Güncel `PlayerAnalysisEngine` mantığı offline CHPP kadrosuna uygulandı. Pozisyon adayları 14 slot için üretildi; oyuncu uygunluğu `PlayerId > 0 && InjuryLevel != 999` kuralıyla sınırlandı. `Biel Kichute` (`InjuryLevel=999`) aday havuzundan çıkarıldı. M3 yalnızca oyuncu profili üretmeye devam ediyor; XI/diziliş/rakip skoru üretmiyor.

### M4 — Aday Diziliş Motoru — PASS ✅

2026-09-01 CHPP offline verisi üzerinde 6 yasal dizilişin tamamı 11 farklı uygun oyuncuyla doldurulabildi. Structural Score sıralaması:

| Sıra | Diziliş | Structural Score | Sonuç |
|---|---|---:|---|
| 1 | **3-5-2** | **17.977** | ✅ |
| 2 | **3-4-3** | **17.907** | ✅ |
| 3 | **2-5-3** | **17.701** | ✅ |
| 4 | **4-5-1** | **17.556** | ✅ |
| 5 | **4-4-2** | **17.365** | ✅ |
| 6 | **5-3-2** | **16.035** | ✅ |

Greedy structural scoring ile optimal distinct-player eşleştirmesi arasındaki fark tüm dizilişlerde çok düşük kaldı; en büyük fark 5-3-2'de yaklaşık **0.059** puan oldu.

**Sonuç: M4 PASS.** Mevcut Motor 4 yapısının değiştirilmesine gerek görülmedi.

### M5 — Pozisyon Optimizasyon Motoru — PARTIAL 🟠

M5'in mevcut `XIOptimizer` kodu gerçek bir takım-seviyesi atama yapıyor: 11 slot için dikdörtgen Hungarian algoritması kullanıyor ve aynı oyuncunun aynı XI içinde iki kez seçilmesini engelliyor. Bu nedenle temel optimizasyon mekanizması çalışıyor.

3-5-2 offline testinde 30 uygun oyuncu içinden 11 farklı oyuncu seçildi ve toplam pozisyon uygunluk skoru **197.77** oldu. Rakip-aware ayarlamayla bulunan eşdeğer optimal çözümün toplamı **198.67** oldu; oyuncu atamasında DEF-CL / DEF-C arasında eşit skorlu bir yer değişimi oluştu.

Test çıktısı:

| Slot | Oyuncu | Suitability |
|---|---|---:|
| GK | Enzo Bultot | 17.90 |
| DEF-CL | Dawid Nocoń | 18.72 |
| DEF-C | Abeiku Takyi | 19.17 |
| DEF-CR | Cristian Pesalovo | 18.39 |
| W-L | Felix Gustavsson | 19.00 |
| IM-L | Francisco Manuel | 17.48 |
| IM-C | Bertalan Doktor | 21.64 |
| IM-R | Milen Bozev | 14.97 |
| W-R | Nándor Dobóvári | 18.94 |
| FW-L | Adrian Beţa | 16.74 |
| FW-R | Ersin Akşın | 14.82 |

**Ancak M5 henüz PASS değil. İki kritik eksik bulundu:**

1. `XIOptimizer.FormationSlots()` şu anda yalnızca **3-5-2** destekliyor. M4 ise 3-5-2, 3-4-3, 4-4-2, 4-5-1, 2-5-3 ve 5-3-2 olmak üzere 6 aday üretiyor. Dolayısıyla M4 → M5 entegrasyonu bütün adaylar için çalışmıyor.
2. `OpponentAdjustment()` mevcut haliyle yalnızca **slot bazlı sabit** bir terim ekliyor; oyuncuya bağlı olmadığı için sabit bir formation içinde Hungarian oyuncu atamasını gerçek anlamda rakibe göre değiştiremiyor. Bu nedenle mevcut kodun “opponent-aware adjustment” iddiası oyuncu seçimi açısından henüz tam karşılanmış sayılmamalı.

**M5 kararı: PARTIAL / FAIL değil, fakat PASS için düzeltme gerekli.** Temel Hungarian optimizasyonu doğrulandı; formation kapsamı ve gerçek rakip-aware oyuncu eşleştirmesi tamamlanmalı.

## Mimari olarak doğrulanan noktalar

1. Motor 3 yalnızca oyuncu profili üretir.
2. Motor 4 yalnızca yasal/doldurulabilir diziliş adaylarını üretir.
3. Motor 3'ün pozisyon uygunluk skoru gerçek maç ratingi değildir.
4. Gerçek CHPP ratingi ile motor tahmini ayrı tutulur.
5. Rakip analizi kendi XI seçimimize bağımlı olmamalıdır.
6. Aynı oyuncu bir aday XI içinde iki slotta kullanılamaz.
7. M4 → M5 geçişinde M5'in tüm M4 formationlarını desteklemesi gerekir.
8. Rakip adjustment oyuncu-slot eşleşmesini gerçekten etkileyebilecek şekilde tasarlanmalıdır; yalnızca slot sabiti eklemek yeterli değildir.
9. Optimizasyon döngüsü yalnızca daha iyi aday bulunduğunda devam etmelidir.
10. Bir motorun sorumluluğu sonraki motorun işini gizlice üstlenmemelidir.

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
