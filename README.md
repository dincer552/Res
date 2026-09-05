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
AŞAMA 6  Web arayüzü ve kullanıcı manueli [TAMAMLANDI]
         - WEB_USER_MANUAL.md
         - WEB_INTERFACE.md
         - WEB_UI_FILE_MAP.md
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
AŞAMA 8  Teknik Manuel PDF birleştirme ve yayın hazırlığı [TAMAMLANDI]
         - 05.09.2026 tarihinde ilk birleşik teknik manuel PDF oluşturuldu.
         - PDF toplam 208 sayfadır.
         - Motor, mimari, gerçek maç, web ve Developer/API bölümleri tek belgede birleştirildi.
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

## TEKNİK MANUEL İÇİNDEKİLER / KAYNAK HARİTASI

Birleşik PDF tek başına kaynak değildir. Aşağıdaki `.md` dosyaları yaşayan teknik kaynaklardır; PDF ise bu kaynakların belirli tarihte alınmış sabit bir yayım/snapshot sürümüdür.

1. `PROJECT_MEMORY.md` — proje hafızası, kararlar, sınırlar ve doğrulanmış proje durumu.
2. `ENGINE_MAP.md` — motorların ve ilgili kod dosyalarının konum haritası.
3. `CHANGE_HISTORY.md` — teknik değişiklik ve acceptance düzeltme geçmişi.
4. `SYSTEM_ARCHITECTURE.md` — backend, pipeline ve runtime mimarisi.
5. `DATA_MODEL.md` — ana veri yapıları, context nesneleri ve veri sözleşmeleri.
6. `MATCH_ENGINE_MATH.md` — Hattrick maç motoru matematiği ve kullanılan referans bağıntılar.
7. `MOTOR_TECHNICAL_MANUAL.md` — M3–M11 motorlarının teknik görevleri, girdileri, çıktıları ve katsayıları.
8. `REAL_MATCH_ANALYSIS.md` — gerçek CHPP offline fixture üzerinden doğrulanmış örnek analiz.
9. `WEB_USER_MANUAL.md` — son kullanıcı için web kullanım akışı.
10. `WEB_INTERFACE.md` — web arayüzünün teknik davranışı.
11. `WEB_UI_FILE_MAP.md` — frontend dosyalarının görev ve bağlantı haritası.
12. `DEVELOPER_API_MANUAL.md` — ASP.NET Core, session, OAuth/CHPP, endpoint'ler ve developer test noktaları.
13. `M8_PHASE_D_PDF_CALIBRATION.md` — M8 PDF/calibration özel teknik notları.

### PDF kaynak snapshot kaydı

- **PDF snapshot tarihi:** 05.09.2026
- **PDF:** `HattrickAI_V5_Teknik_Manuel_A8_FINAL.pdf`
- **PDF sayfa sayısı:** 208
- **Kaynak indexi:** `HattrickAI_V5/Docs/TECHNICAL_MANUAL_INDEX.md`

PDF yeniden oluşturulduğunda snapshot tarihi güncellenecek ve `TECHNICAL_MANUAL_INDEX.md` içindeki kaynak tarihleri yeniden kaydedilecek. Böylece herhangi bir PDF sürümünün hangi Markdown bilgi snapshot'ından üretildiği takip edilebilecek.

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
- **05.09.2026 — AŞAMA 6:** `wwwroot/index.html`, `motor-render.js` ve diğer gerçek frontend yardımcı dosyalarının görevleri dokümante edildi. `WEB_USER_MANUAL.md`, `WEB_INTERFACE.md` ve `WEB_UI_FILE_MAP.md` tamamlandı.
- **05.09.2026 — AŞAMA 7:** `Program.cs`, `ChppV5.cs` ve `AnalysisService.cs` üzerinden backend HTTP sınırı, session/OAuth akışı, CHPP XML client, production endpoint'leri, analysis data flow, historical opponent reconstruction ve HTTP hata davranışları `DEVELOPER_API_MANUAL.md` içine işlendi.
- **05.09.2026 — AŞAMA 8:** Birleşik teknik manuel PDF oluşturuldu. 208 sayfalık derleme; mimari, veri modeli, matematik, M3–M11 motorları, gerçek maç fixture'ı, web arayüzü ve Developer/API bölümlerini içerir.

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
- `HattrickAI_V5/Docs/M8_PHASE_D_PDF_CALIBRATION.md`
- `HattrickAI_V5/Docs/TECHNICAL_MANUAL_INDEX.md`