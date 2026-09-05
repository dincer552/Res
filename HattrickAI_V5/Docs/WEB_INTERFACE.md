# HattrickAI V5 — Web Arayüzü Teknik Manueli

**Durum:** Kaynak koduna dayalı dokümantasyon  
**Branch:** `v5`  
**Kaynak:** `HattrickAI_V5/wwwroot/index.html`, `HattrickAI_V5/wwwroot/motor-render.js`, `HattrickAI_V5/Program.cs`  

## 1. Amaç

Bu belge HattrickAI V5 web arayüzünün mevcut implementasyonunu geliştirici ve kullanıcı açısından açıklar. Buradaki davranışlar yalnızca repository'deki gerçek frontend/backend kodundan çıkarılmıştır.

Bu belge, `WEB_USER_MANUAL.md` içindeki kullanıcı akışını tamamlayan daha teknik bir UI referansıdır.

## 2. Sayfa yapısı

Ana sayfa `HattrickAI_V5/wwwroot/index.html` içinde tanımlıdır. Sayfa dar ekranlara da uyarlanmış, tek kolonlu bir arayüz kullanır.

Ana bölümler:

1. Üst bar
2. CHPP bağlantı durumu
3. Maç analizi paneli
4. Üç soruluk maç koşulu formu
5. Kullanıcı takımının önerilen 11'i
6. Rakip takımın son maç 11'i
7. Runtime durum satırı

### Üst bar

Üst barda:

- `HattrickAI` marka adı
- build bilgisi (`#build`)
- `CHPP bağlan` butonu (`#connect`)

bulunur.

Backend `/api/v5/build` endpoint'i üzerinden build değeri sağlanır. Backend tarafında `V5_BUILD`, `BUILD_SHA` veya `GITHUB_SHA` ortam değişkenlerinden biri kullanılır; değer 7 karaktere kısaltılır.

## 3. CHPP bağlantı durumu

Arayüzde `#statusDot`, `#statusTitle` ve `#statusText` alanları bağlantı durumunu gösterir.

`/api/v5/status` endpoint'i:

- `connected`: session içinde access token ve access secret bulunup bulunmadığını,
- `configured`: CHPP consumer secret'ın yapılandırılmış olup olmadığını

döndürür.

CHPP bağlantısı yokken analiz endpoint'i `401 Unauthorized` döndürür.

## 4. Analiz paneli

Ana analiz düğmesi:

`#analyze` → `ANALİZİ ÇALIŞTIR`

Sayfa ilk açıldığında buton disabled durumundadır. CHPP bağlantısı ve arayüz akışı uygun hale geldiğinde analiz başlatılır.

Analiz sırasında:

`CHPP verileri okunuyor ve bölgesel rating hesaplanıyor…`

mesajı gösterilir.

Hata için `#error` alanı kullanılır ve hata metni kullanıcıya gösterilir.

## 5. Üç soruluk form

Analiz öncesi arayüz üç kullanıcı girdisi toplar.

### Soru 1 — Teknik direktör tarzı

Gerçek seçenekler:

| API değeri | Ekran değeri |
|---|---|
| `Neutral` | Dengeli |
| `Offensive` | Hücum |
| `Defensive` | Defans |

### Soru 2 — Takım ruhu

Kodda bulunan seçenekler:

- `Murderous` — Öldürücü
- `Furious` — Öfkeli
- `Irritated` — Sinirli
- `Composed` — Soğukkanlı
- `Calm` — Sakin
- `Content` — Memnun
- `Satisfied` — Tatmin olmuş
- `Delirious` — Coşkulu
- `WalkingOnClouds` — Bulutların üzerinde
- `ParadiseOnEarth` — Cennette

### Soru 3 — Maç önemi / yaklaşımı

Bu alan frontend tarafından `matchImportance` anahtarıyla backend'e gönderilir ve backend bunu `TeamAttitude` enum'una parse eder.

Kodda varsayılan değer `TeamAttitude.Normal`'dır.

> Seçeneklerin tam kullanıcı etiketleri bu dokümanda yalnızca kaynak kodundan doğrulanabildiği ölçüde belirtilmelidir; kodun ilgili kısmı değişirse bu bölüm de güncellenmelidir.

## 6. Questionnaire API

Frontend cevapları:

`POST /api/v5/questionnaire`

endpoint'ine gönderir.

Backend:

1. `CoachStyle` parse eder.
2. `TeamSpiritLevel` parse eder.
3. `TeamAttitude` parse eder.
4. Üç değeri HTTP session'a kaydeder.

Session anahtarları:

- `v5.coach`
- `v5.spirit`
- `v5.attitude`

`GET /api/v5/questionnaire` mevcut session değerlerini döndürür; değer yoksa sırasıyla `Neutral`, `Composed`, `Normal` varsayılanları kullanılır.

## 7. Analiz request akışı

Frontend'in analiz isteği backend'deki:

`GET /api/v5/analysis`

endpoint'ine ulaşır.

Backend akışı özetle:

```text
Browser
  ↓
/api/v5/analysis
  ↓
CHPP bağlantı kontrolü
  ↓
AnalysisService.RunAsync
  ↓
CHPP teamdetails
  ↓
CHPP training
  ↓
CHPP players
  ↓
CHPP matches
  ↓
seçilen gelecek lig maçı
  ↓
rakibin matches
  ↓
rakibin son resmi maçındaki lineup
  ↓
rakip matchdetails ratingleri
  ↓
MotorPipelineService
  ↓
FinalPlan
  ↓
JSON Analysis sonucu
  ↓
frontend render
```

## 8. Kullanıcı takımı kartı

Kullanıcı takım kartı `KULLANICI TAKIMI` başlığıyla gösterilir.

Analiz tamamlandığında:

- takım adı
- önerilen XI
- oyuncu pozisyonları
- oyuncu ratingleri
- önerilen diziliş
- bölgesel ratingler

gösterilir.

Saha üzerinde kullanılan gerçek slot kodları:

- `GK`
- `DEF-L`, `DEF-CL`, `DEF-C`, `DEF-CR`, `DEF-R`
- `W-L`, `IM-L`, `IM-C`, `IM-R`, `W-R`
- `FW-L`, `FW-C`, `FW-R`

14 görsel slot tanımlı olmakla birlikte gerçek diziliş yalnızca seçilen 11 oyuncuyu doldurur.

## 9. Rakip takım kartı

Rakip kartı `RAKİP TAKIMI` başlığıyla gösterilir.

Kodun gerçek akışında rakibin:

1. yaklaşan maçtaki rakip ID'si bulunur,
2. rakibin maç geçmişi okunur,
3. son tamamlanmış rekabetçi maç seçilir,
4. `matchlineup` ile saha oyuncuları okunur,
5. substitutions dikkate alınarak final saha 11'i oluşturulur,
6. `matchdetails` ile tarihsel ratingler okunur.

Arayüzde rakibin son maç yerleşimi ve ratingleri gösterilir.

## 10. Rating panosu

`motor-render.js` tarafında rating board bölgesel değerleri sahada gösterir.

Bölgesel yapı:

- Left Defence
- Centre Defence
- Right Defence
- Midfield
- Left Attack
- Centre Attack
- Right Attack

Kullanıcı takımında rating alanı `RP` olarak render edilir.

Rakip takımında son maç yıldız verisi geçerliyse `SP`, aksi durumda `RP` kullanılır.

## 11. Oyuncu davranış emirleri

Frontend gerçek davranış değerlerini Türkçe etiketlere dönüştürür:

| Değer | Ekran |
|---:|---|
| 0 | NORMAL |
| 1 | OFANSİF |
| 2 | DEFANSİF |
| 3 | MERKEZE |
| 4 | KANA |

Bu eşleme oyuncunun saha üzerindeki pozisyonundan ayrı bir davranış/order bilgisidir.

## 12. Önerilen diziliş ile taktik arasındaki fark

Arayüzde görülen `Önerilen diziliş`, motor pipeline'ının seçtiği final lineup/formation bilgisidir.

Bu değer `TeamTactic` ile aynı şey değildir.

Mevcut production analysis path içinde `AnalysisService` `RatingContext` oluştururken `TeamTactic.Normal` gönderir. `AdvancedTacticalScenarioEngine` verilen taktiğin etkilerini hesaplar; bağımsız bir taktik seçimi yapmaz.

Bu nedenle UI'da motorun hesapladığı final taktikmiş gibi `ORTADAN ATAK` veya `KANATTAN ATAK` göstermek mevcut kod tarafından desteklenmemektedir.

Gerçek mevcut durumun teknik karşılığı `TeamTactic.Normal` inputudur.

## 13. Baz alınan maç kutusu

Rakip kartının altında `BAZ ALINAN MAÇ` kutusu bulunur.

Bu alan backend `/api/v5/reference-match` endpoint'inden alınan:

- rakip maç adı
- skor
- maç tipi
- tarih
- tamamlanmışsa kazanan/kaybeden bilgisi

ile doldurulur.

Bu kutu frontend tarafında analiz runtime alanında `analiz tamamlandı` değişimini gözlemleyerek yüklenir.

## 14. Copy butonları

Kullanıcı ve rakip kartlarında `KOPYALA` butonları bulunur.

Bunların davranışı `motor-render.js` tarafından yönetilir. Başarılı kopyalama durumunda buton `ok` sınıfına geçer.

## 15. Runtime ve motor logları

Backend session bazlı motor çalışma logu tutar.

Endpoint:

`GET /api/v5/motor-logs`

Log yoksa:

`{ available: false }`

Log varsa:

`{ available: true, log: ... }`

şeklinde sonuç döner.

Ayrıca ana sayfaya deploy logları backend middleware tarafından dinamik olarak enjekte edilir. Bu panel kapalı/açılır yapıdadır ve `/api/deploy/log` endpoint'ini kullanır.

## 16. Manuel deploy arayüzü

Ana sayfadaki deploy panelinde `🚀 Manuel Deploy` düğmesi bulunur.

Backend:

`POST /api/deploy/manual`

endpoint'i üzerinden GitHub Actions `v5-build.yml` workflow'unu `v5` branch'i için tetikler.

Endpoint:

- CHPP bağlantısı yoksa `401`
- `GITHUB_ACTIONS_TOKEN` yoksa `503`
- GitHub dispatch başarısızsa `502`
- başarılı dispatch'te `200`

döndürebilir.

Bu deploy paneli maç analiz motorunun bir parçası değildir; operasyon/deployment yardımcı arayüzüdür.

## 17. CHPP bağlantısının teknik modeli

CHPP client `HattrickAI_V5/Core/ChppV5.cs` içinde bulunur.

Kullanılan OAuth akışı:

```text
CHPP request token
      ↓
Hattrick authorization
      ↓
verifier
      ↓
CHPP access token
      ↓
HTTP session
      ↓
OAuth-signed CHPP XML requests
```

Session'da access token ve secret tutulur; frontend bu gizli değerleri doğrudan görmez.

## 18. Responsive arayüz

CSS içinde küçük ekranlar için `@media(max-width:420px)` kuralları vardır.

Bu kurallar özellikle:

- sayfa padding'i
- üst bar
- bağlantı butonu
- başlıklar
- saha çizimleri
- oyuncu slotları
- rating board
- soru kartı
- copy butonları

gibi alanların küçük ekranlarda küçültülmesini sağlar.

## 19. UI durumları

Arayüzde görülen ana durumlar:

### Başlangıç

`CHPP bağlantısı kontrol ediliyor`

### Bağlantısız

Kullanıcıdan Hattrick hesabını bağlaması istenir.

### Soru formu

Üç maç koşulu sorusu sırayla gösterilir.

### Analiz çalışıyor

CHPP verileri okunur ve motor pipeline çalışır.

### Başarılı

Önerilen XI, ratingler, rakip ve diziliş bilgileri render edilir.

### Hata

Backend hatası kullanıcıya hata kutusunda gösterilir ve runtime durumu başarısız olarak işaretlenebilir.

## 20. UI ile backend arasındaki endpoint tablosu

| Endpoint | Method | Amaç |
|---|---|---|
| `/health` | GET | servis health/build bilgisi |
| `/api/v5/build` | GET | build bilgisi |
| `/api/v5/status` | GET | CHPP bağlantı/config durumu |
| `/api/v5/questionnaire` | GET | session questionnaire değerleri |
| `/api/v5/questionnaire` | POST | questionnaire kaydetme |
| `/api/v5/analysis` | GET | canlı V5 analizini çalıştırma |
| `/api/v5/offline-export` | GET | offline export oluşturma |
| `/api/v5/reference-match` | GET | rakip baz maçını alma |
| `/api/v5/motor-logs` | GET | son motor çalışma logu |
| `/api/deploy/log` | GET | deploy logları |
| `/api/deploy/manual` | POST | v5 deploy workflow dispatch |
| `/auth/chpp/start` | GET | CHPP OAuth başlangıcı |
| `/auth/chpp/callback` | GET | CHPP OAuth callback |
| `/auth/chpp/disconnect` | GET | CHPP session tokenlarını temizleme |

## 21. Gerçek kodda olmayan UI davranışları

Aşağıdakiler mevcut source'a dayanarak gerçeklenmiş özellik olarak dokümante edilmemelidir:

- bağımsız taktik selector sonucu,
- motorun `AttackMiddle` / `AttackWings` seçtiği iddiası,
- frontend'in M8/M9/M10/M11 için olmayan sonuçları kendisinin üretmesi,
- CHPP tokenlarının browser JavaScript'ine açıkça verilmesi,
- backend'in yapmadığı bir hesaplamanın UI tarafından yapılması.

## 22. Kaynak dosyalar

- `HattrickAI_V5/wwwroot/index.html`
- `HattrickAI_V5/wwwroot/motor-render.js`
- `HattrickAI_V5/Program.cs`
- `HattrickAI_V5/Core/ChppV5.cs`
- `HattrickAI_V5/Core/AnalysisService.cs`

İlgili üst seviye belgeler:

- `HattrickAI_V5/Docs/WEB_USER_MANUAL.md`
- `HattrickAI_V5/Docs/DEVELOPER_API_MANUAL.md`
- `HattrickAI_V5/Docs/SYSTEM_ARCHITECTURE.md`
- `HattrickAI_V5/Docs/MOTOR_TECHNICAL_MANUAL.md`
