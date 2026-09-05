# HattrickAI V5

## V5 Teknik Manuel PDF Projesi — Çalışma Planı

Bu bölüm HattrickAI V5'in gerçek kod, test ve kaynak dokümanlarından oluşturulacak teknik manuel PDF çalışmasının takip alanıdır.

Amaç: V5 motorlarının yaptığı işlemleri, kullanılan matematikleri, katsayıları, veri akışlarını ve web kullanımını sadece mevcut kaynaklara dayanarak dokümante etmek.

### Manuel hazırlama aşamaları

```
AŞAMA 0  Kaynak envanteri                         [TAMAMLANDI]
AŞAMA 0.5 Motor / Kod Konum Haritası             [KISMEN TAMAMLANDI]
AŞAMA 1  Sistem mimarisi                         [TAMAMLANDI]
         - Genel V5 akışı
         - Veri giriş/çıkış zinciri
         - Motorlar arası bağlantılar

AŞAMA 2  Veri modeli
         - Player modeli
         - Team modeli
         - Match modeli
         - Database yapıları

AŞAMA 3  Hattrick matematik modeli
         - Rating hesapları
         - Possession hesapları
         - Attack/defence bölgesel hesapları
         - Taktik dönüşümleri

AŞAMA 4  Motor teknik dokümanları
         - M3
         - M4
         - M5
         - M6-A
         - M7
         - M7.2
         - M8
         - M9
         - DB1
         - M10
         - M6-B
         - DB2
         - M11

AŞAMA 5  Gerçek maç örnek analizi

AŞAMA 6  Web kullanıcı manueli

AŞAMA 7  Developer/API manueli
```

Her aşama tamamlandığında bu bölüm güncellenecek ve hazırlanan PDF bölümleri manuel içerisine eklenecek.

Dokümantasyon prensibi:
- Tahmin edilen veya varsayılan hesap yazılmayacak.
- Kullanılan katsayılar sadece kod/config veya kaynak PDF'den alınacak.
- Her motor için input, output, hesaplama mantığı ve kullanılan dosya yolu belirtilecek.

### Motor / Kod ilişkilendirme standardı

Her motor dokümanında aşağıdaki bilgiler bulunacak:

- GitHub dosya yolu
- Class adı
- Ana çalışan fonksiyonlar
- Aldığı veri (input)
- Ürettiği veri (output)
- Hesaplama mantığı
- Kullandığı katsayılar ve kaynakları
- Önceki ve sonraki motor bağlantısı

Örnek dokümantasyon zinciri:

```
M3
 |
 | input/output
 v
M4
 |
 v
M5
 |
 v
M6-A
 |
 v
...
 |
 v
M11
 |
 v
FinalPlan
```

Bu bölüm PDF manuelinde geliştirici referansı olarak kullanılacaktır.

---

## AŞAMA 1 DURUMU — SİSTEM MİMARİSİ

Aşama 1'in doğrulanmış ayrıntılı dokümantasyonu:

`HattrickAI_V5/Docs/SYSTEM_ARCHITECTURE.md`

Bu belge production girişinden CHPP veri toplama, MatchDataContext oluşturma, M3-M11 pipeline, DB1/DB2 ve frontend'e dönüş zincirini açıklar.

Önemli mimari ayrımlar da burada kayıtlıdır:

- M4 legal formation adaylarını üretir.
- M5 oyuncu-slot eşleştirmelerini optimize eder.
- M6 davranış araması yapar ve downstream evaluator'ı çalıştırır.
- M7 rating scenario üretir.
- M7.2 ve M8 verilen taktiğin sonuçlarını hesaplar; team tactic seçmez.
- M10 formasyon yarışması/final karar ve TeamAttitude tarafını yönetir.
- M6-B M10 formation rank'e göre refinement yapar.
- M11 DB2 final seçimidir.
- Mevcut web production path `TeamTactic.Normal` ile başlar.

Aşama 1'de doğrulanmamış ayrıntılar varsayımla doldurulmamıştır.

---

## DOĞRULANMIŞ TEKNİK NOTLAR — 05.09.2026

### Taktik hesaplama / seçim durumu

Kod incelemesi sonucunda mevcut web production analiz akışında takım taktiğini seçen ayrı bir motor/selector bulunmadığı doğrulandı.

`HattrickAI_V5/Core/AnalysisService.cs` içinde `RatingContext` oluşturulurken `TeamTactic.Normal` veriliyor.

`HattrickAI_V5/Core/MotorPipelineService.cs` bu değeri `MatchState` üzerinden M7/M7.2/M8 hesaplarına taşıyor.

`HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs` verilen taktiğin etkilerini hesaplıyor; taktik seçmiyor.

`HattrickAI_V5/Core/M8ChanceAllocationEngine.cs` verilen taktiğe göre dönüşüm ve şans dağılımı hesaplıyor; taktik seçmiyor.

`HattrickAI_V5/Core/M10FinalDecisionEngine.cs` final plan/formasyon ve `TeamAttitude` seçebiliyor. `TeamAttitude`, `TeamTactic` değildir.

Sonuç: UI'da `ORTADAN ATAK`, `KANATTAN ATAK` vb. değerleri motorun hesapladığı final taktikmiş gibi göstermek doğru değildir. Mevcut web path için gerçek durum `TAKTİK YOK` (altındaki input değeri `TeamTactic.Normal`) olarak dokümante edilmelidir.

Bu durum gerçek bir taktik selector bulunana kadar açık teknik gap olarak tutulacaktır.

---

## SON İŞLEMLER — 05.09.2026

- **05.09.2026 — PRODUCTION DEPLOY:** V5 Docker build ve Azure deployment başarıyla tamamlandı; deployment health check doğrulandı.
- **05.09.2026 — REGRESSION TESTLERİ:** C1–C18 offline acceptance/regression çalıştırması şimdilik durduruldu. Deployment artık regression gate'e bağlı olmadan devam ediyor.
- **05.09.2026 — C12:** M6-B refinement acceptance doğrulandı: DB2=100, 6 formasyon, 6 bütçe, 23701 değerlendirme.
- **05.09.2026 — C13:** DB2 formation coverage düzeltildi; acceptance artık production DB2=100 içinden exposed DB2=90 kapsamını doğru kabul ediyor. 6 yasal formasyonun tamamı kapsanıyor.
- **05.09.2026 — C14:** M11 finalist pool ve M11 telemetry doğrulaması düzeltildi; M11 finalist pool 90 aday / 6 formasyon olarak geçiyor.
- **05.09.2026 — C15:** M11 final selection testindeki top-N ranking davranışıyla ilgili acceptance uyumsuzluğu giderildi; ranking top-N mantığı production davranışıyla hizalandı.
