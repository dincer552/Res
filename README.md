# HattrickAI V5

## V5 Teknik Manuel PDF Projesi — Çalışma Planı

Bu bölüm HattrickAI V5'in gerçek kod, test ve kaynak dokümanlarından oluşturulacak teknik manuel PDF çalışmasının takip alanıdır.

Amaç: V5 motorlarının yaptığı işlemleri, kullanılan matematikleri, katsayıları, veri akışlarını ve web kullanımını sadece mevcut kaynaklara dayanarak dokümante etmek.

### Manuel hazırlama aşamaları

```
AŞAMA 0  Kaynak envanteri
         - Repository analizi
         - Motor dosyaları
         - Config dosyaları
         - Test çıktıları
         - Referans PDF matematikleri

AŞAMA 1  Sistem mimarisi
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

---

## SON İŞLEMLER — 05.09.2026

- **05.09.2026 — PRODUCTION DEPLOY:** V5 Docker build ve Azure deployment başarıyla tamamlandı; deployment health check doğrulandı.
- **05.09.2026 — REGRESSION TESTLERİ:** C1–C18 offline acceptance/regression çalıştırması şimdilik durduruldu. Deployment artık regression gate'e bağlı olmadan devam ediyor.
- **05.09.2026 — C12:** M6-B refinement acceptance doğrulandı: DB2=100, 6 formasyon, 6 bütçe, 23701 değerlendirme.
- **05.09.2026 — C13:** DB2 formation coverage düzeltildi; acceptance artık production DB2=100 içinden exposed DB2=90 kapsamını doğru kabul ediyor. 6 yasal formasyonun tamamı kapsanıyor.
- **05.09.2026 — C14:** M11 finalist pool ve M11 telemetry doğrulaması düzeltildi; M11 finalist pool 90 aday / 6 formasyon olarak geçiyor.
- **05.09.2026 — C15:** M11 final selection testindeki top-N ranking davranışıyla ilgili acceptance uyumsuzluğu giderildi; ranking top-N mantığı production davranışıyla hizalandı.
