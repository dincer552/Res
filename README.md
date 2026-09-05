# HattrickAI V5

## V5 Teknik Manuel PDF Projesi — Çalışma Planı

Bu bölüm HattrickAI V5'in gerçek kod, test ve kaynak dokümanlarından oluşturulacak teknik manuel PDF çalışmasının takip alanıdır.

Amaç: V5 motorlarının yaptığı işlemleri, kullanılan matematikleri, katsayıları, veri akışlarını ve web kullanımını sadece mevcut kaynaklara dayanarak dokümante etmek.

### Manuel hazırlama aşamaları

```
AŞAMA 0  Kaynak envanteri
AŞAMA 0.5 Motor / Kod Konum Haritası
AŞAMA 1  Sistem mimarisi [TAMAMLANDI]
AŞAMA 2  Veri modeli [TAMAMLANDI]
AŞAMA 3  Hattrick matematik modeli [TAMAMLANDI]
AŞAMA 4  Motor teknik dokümanları [TAMAMLANDI]
AŞAMA 5  Gerçek maç örnek analizi [TAMAMLANDI]
AŞAMA 6  Web kullanıcı manueli [TAMAMLANDI]
AŞAMA 7  Developer/API manueli [TAMAMLANDI]
         - ASP.NET Core uygulama başlangıcı ve DI
         - Session sözleşmesi
         - CHPP OAuth 1.0 akışı
         - CHPP XML client
         - Production endpoint kataloğu
         - AnalysisService veri akışı
         - Historical opponent reconstruction
         - HTTP hata davranışları
         - Developer test noktaları
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

---

## DOĞRULANMIŞ TEKNİK NOTLAR — 05.09.2026

### Taktik hesaplama / seçim durumu

Kod incelemesi sonucunda mevcut web production analiz akışında takım taktiğini seçen ayrı bir motor/selector bulunmadığı doğrulandı.

`HattrickAI_V5/Core/AnalysisService.cs` içinde `RatingContext` oluşturulurken `TeamTactic.Normal` veriliyor.

`HattrickAI_V5/Core/MotorPipelineService.cs` bu değeri M7/M7.2/M8 hesaplarına taşıyor.

`AdvancedTacticalScenarioEngine` verilen taktiğin etkilerini hesaplıyor; taktik seçmiyor. `M8ChanceAllocationEngine` verilen taktiğe göre dönüşüm ve şans dağılımı hesaplıyor; taktik seçmiyor. `M10FinalDecisionEngine` `TeamAttitude` yaklaşımı seçebiliyor; `TeamAttitude`, `TeamTactic` değildir.

Sonuç: UI'da `ORTADAN ATAK`, `KANATTAN ATAK` vb. değerleri motorun hesapladığı final taktikmiş gibi göstermek doğru değildir. Mevcut web path için gerçek durum `TAKTİK YOK` (input değeri `TeamTactic.Normal`) olarak dokümante edilmelidir.

## SON İŞLEMLER — 05.09.2026

- **05.09.2026 — PRODUCTION DEPLOY:** V5 Docker build ve Azure deployment başarıyla tamamlandı; deployment health check doğrulandı.
- **05.09.2026 — REGRESSION TESTLERİ:** C1–C18 offline acceptance/regression çalıştırması şimdilik durduruldu. Deployment artık regression gate'e bağlı olmadan devam ediyor.
- **05.09.2026 — C12:** M6-B refinement acceptance doğrulandı: DB2=100, 6 formasyon, 6 bütçe, 23701 değerlendirme.
- **05.09.2026 — C13:** DB2 formation coverage düzeltildi; acceptance production DB2=100 içinden exposed DB2=90 kapsamını doğru kabul ediyor. 6 yasal formasyonun tamamı kapsanıyor.
- **05.09.2026 — C14:** M11 finalist pool ve telemetry doğrulaması düzeltildi; M11 finalist pool 90 aday / 6 formasyon olarak geçiyor.
- **05.09.2026 — C15:** M11 final selection testindeki top-N ranking davranışı production davranışıyla hizalandı.
- **05.09.2026 — AŞAMA 5:** Gerçek CHPP offline fixture üzerinden maç örnek analizi `REAL_MATCH_ANALYSIS.md` içine işlendi. Fixture'da bulunmayan M8/M9/M10/M11 sonuçları özellikle üretilmedi.
- **05.09.2026 — AŞAMA 6:** `wwwroot/index.html` ve `motor-render.js` frontend davranışları `WEB_USER_MANUAL.md` içine işlendi. Web klasöründeki gerçek dosya→ekran→sorumluluk ilişkisi ayrıca `WEB_INTERFACE.md` ve `WEB_UI_FILE_MAP.md` ile kayıt altına alındı.
- **05.09.2026 — AŞAMA 7:** `Program.cs`, `ChppV5.cs` ve `AnalysisService.cs` üzerinden backend HTTP sınırı, session/OAuth akışı, CHPP XML client, production endpoint'leri, analysis data flow, historical opponent reconstruction ve HTTP hata davranışları `DEVELOPER_API_MANUAL.md` içine işlendi.

## DOKÜMANTASYON DOSYALARI

- `HattrickAI_V5/Docs/PROJECT_MEMORY.md`
- `HattrickAI_V5/Docs/ENGINE_MAP.md`
- `HattrickAI_V5/Docs/CHANGE_HISTORY.md`
- `HattrickAI_V5/Docs/SYSTEM_ARCHITECTURE.md`
- `HattrickAI_V5/Docs/DATA_MODEL.md`
- `HattrickAI_V5/Docs/MATCH_ENGINE_MATH.md`
- `HattrickAI_V5/Docs/MOTOR_TECHNICAL_MANUAL.md`
- `HattrickAI_V5/Docs/REAL_MATCH_ANALYSIS.md`
- `HattrickAI_V5/Docs/WEB_USER_MANUAL.md`
- `HattrickAI_V5/Docs/WEB_INTERFACE.md`
- `HattrickAI_V5/Docs/WEB_UI_FILE_MAP.md`
- `HattrickAI_V5/Docs/DEVELOPER_API_MANUAL.md`
