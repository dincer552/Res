# HattrickAI V5 — Developer / API Manueli

**Aşama 7 — 05.09.2026**

Bu belge yalnızca mevcut `v5` branch'indeki gerçek uygulama kodunu temel alır. Endpoint, veri alanı, authentication davranışı veya motor çıktısı kodda görülmüyorsa burada tanımlanmaz.

## 1. Kapsam

V5 web uygulaması ASP.NET Core minimal API yapısındadır. HTTP endpoint'leri `HattrickAI_V5/Program.cs` içinde map edilir. CHPP erişimi `HattrickAI_V5/Core/ChppV5.cs`, ana analiz orkestrasyonu `HattrickAI_V5/Core/AnalysisService.cs`, motor zinciri ise `MotorPipelineService` üzerinden yürür.

Ana zincir:

```text
Browser
  |
  +-- /auth/chpp/start
  +-- /auth/chpp/callback
  +-- /auth/chpp/disconnect
  |
  +-- /api/v5/status
  +-- /api/v5/questionnaire
  +-- /api/v5/analysis
  +-- /api/v5/offline-export
  +-- /api/v5/reference-match
  +-- /api/v5/motor-logs
  |
  v
AnalysisService
  |
  v
MotorPipelineService
  |
  v
M3 -> M4 -> M5 -> M6-A -> M7 -> M7.2 -> M8 -> M9 -> DB1 -> M10 -> M6-B -> DB2 -> M11
```

## 2. Uygulama başlatma ve DI

`Program.cs`:

- JSON HTTP seçeneklerinde camelCase serialization etkinleştirilir.
- `IHttpContextAccessor` eklenir.
- distributed memory cache ve ASP.NET Session etkinleştirilir.
- Session cookie adı `hattrickai.v5`.
- Cookie `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always`.
- Session idle timeout 8 saattir.
- `ChppV5`, `AnalysisService` ve `ReferenceMatchService` scoped olarak kaydedilir.
- HTTP portu `PORT` environment variable üzerinden alınır; parse edilemezse 10000 kullanılır.
- uygulama `0.0.0.0:<port>` adresine bind edilir.

## 3. Environment / secret sözleşmesi

Kod tarafından okunan deployment değişkenleri:

| Değişken | Kullanım |
|---|---|
| `CHPP_CONSUMER_KEY` | CHPP consumer key; boşsa kod içindeki embedded consumer key kullanılır |
| `CHPP_CONSUMER_SECRET` | CHPP OAuth consumer secret; CHPP login ve `/api/v5/status` için gerekli |
| `GITHUB_ACTIONS_TOKEN` | `/api/deploy/manual` endpoint'inin GitHub workflow dispatch çağrısı |
| `V5_BUILD` | build etiketi |
| `BUILD_SHA` | `V5_BUILD` yoksa build etiketi |
| `GITHUB_SHA` | önceki iki değer yoksa build etiketi |
| `PORT` | HTTP listen portu |

Consumer secret ve GitHub token kaynak koduna yazılmaz. Consumer key için mevcut kodda fallback değer bulunur.

## 4. Session sözleşmesi

`ChppV5` ve `Program.cs` tarafından kullanılan session anahtarları:

| Key | Değer |
|---|---|
| `v5.request` | OAuth request token |
| `v5.requestSecret` | OAuth request token secret |
| `v5.access` | OAuth access token |
| `v5.accessSecret` | OAuth access token secret |
| `v5.coach` | `CoachStyle` enum adı |
| `v5.spirit` | `TeamSpiritLevel` enum adı |
| `v5.attitude` | `TeamAttitude` enum adı |

Bağlantı durumu `v5.access` ve `v5.accessSecret` ikisinin de dolu olmasına bağlıdır.

## 5. CHPP authentication

### 5.1 Başlangıç

`GET /auth/chpp/start`:

1. `CHPP_CONSUMER_SECRET` yoksa kullanıcı root'a hata query'si ile yönlendirilir.
2. `X-Forwarded-Proto` varsa kullanılır; yoksa request scheme kullanılır.
3. callback adresi `<proto>://<host>/auth/chpp/callback` şeklinde oluşturulur.
4. `ChppV5.StartAsync()` OAuth request token ister.
5. Request token ve secret session'a yazılır.
6. Hattrick authorize URL'sine yönlendirilir.
7. Authorization URL'sinde scope olarak `set_matchorder,manage_youthplayers` gönderilir.

### 5.2 Request token imzası

`ChppV5` OAuth 1.0 kullanır:

- signature method: `HMAC-SHA1`
- version: `1.0`
- timestamp: UTC Unix time
- nonce: 16 random byte, hex
- OAuth signature base string: HTTP method + base URL + normalized parameters
- signing key: encoded consumer secret + `&` + encoded token secret

İlk request query parametreleriyle imzalanır. HTTP hata dönerse aynı istek Authorization header kullanılarak tekrar denenir.

### 5.3 Callback / access token

`GET /auth/chpp/callback` verifier'ı alır ve `ChppV5.FinishAsync()` çağrılır. Request token/session secret kullanılarak access token alınır. Başarılı olduğunda access token ve access secret session'a yazılır; request token bilgileri silinir.

Kod, verifier içindeki `#_=_` ekini temizler.

### 5.4 Disconnect

`GET /auth/chpp/disconnect` mevcut OAuth session anahtarlarını kaldırır. CHPP hesabının kendisinde bir revoke işlemi yapılmaz.

## 6. CHPP API client

`ChppV5.GetXmlAsync(file, parameters, ct)` yalnızca bağlı session üzerinden çalışır.

İstek:

- endpoint: `https://chpp.hattrick.org/chppxml.ashx`
- `file` parametresi zorunlu olarak eklenir.
- boş parameter değerleri atılır.
- OAuth Authorization header eklenir.
- User-Agent: `HattrickAI, v18.0`
- Accept: XML
- HTTP/1.1 exact request policy
- timeout: 30 saniye
- gzip/deflate decompression açık
- automatic redirect kapalı
- cookies HttpClient seviyesinde kullanılmaz

HTTP status başarılı değilse response body ile `HttpRequestException` üretilir.

## 7. Production endpoint kataloğu

### `GET /health`

Health response'u:

```json
{
  "ok": true,
  "service": "HattrickAI V5",
  "build": "<build>"
}
```

### `GET /api/v5/build`

```json
{
  "build": "<build>"
}
```

Build değeri `V5_BUILD`, sonra `BUILD_SHA`, sonra `GITHUB_SHA`, son olarak `dev` kaynaklarından alınır ve 7 karakterden uzunsa ilk 7 karaktere kesilir.

### `GET /api/v5/status`

Bağlantı ve secret konfigürasyonunu döndürür:

```json
{
  "connected": true,
  "configured": true
}
```

`connected`, session access token + access secret varlığıdır. `configured`, `CHPP_CONSUMER_SECRET` değerinin boş olmamasıdır.

### `GET /api/v5/questionnaire`

Session'daki üç seçimi `MatchQuestionnaire` olarak döndürür. Session değeri yoksa varsayılanlar:

- `CoachStyle.Neutral`
- `TeamSpiritLevel.Composed`
- `TeamAttitude.Normal`

### `POST /api/v5/questionnaire`

Body'de şu alanlar beklenir:

```json
{
  "coachStyle": "Neutral",
  "teamSpirit": "Composed",
  "matchImportance": "Normal"
}
```

Üç değer de enum olarak parse edilir. Geçersiz değer 400 döndürür. Başarılı istek üç session anahtarını günceller ve `{ "ok": true }` döndürür.

### `GET /api/v5/analysis`

CHPP bağlantısı yoksa 401.

Bağlıysa:

1. session için motor run kaydı başlatılır.
2. M3 başlangıç logu yazılır.
3. questionnaire session'dan oluşturulur.
4. `AnalysisService.RunAsync(build, questionnaire, ct)` çağrılır.
5. başarıda motor run `Analiz tamamlandı` ile tamamlanır.
6. `Analysis` nesnesi JSON olarak döner.
7. exception durumunda motor run başarısız olarak işaretlenir ve 502 döner.

### `GET /api/v5/offline-export`

CHPP bağlantısı yoksa 401. Questionnaire session'dan alınır, `OfflineExportService.ExportAsync()` çağrılır ve sonuç JSON olarak döner. `UnauthorizedAccessException` 401, diğer exception'lar 502 olur.

### `GET /api/v5/reference-match`

Bağlı kullanıcı için `ReferenceMatchService.GetAsync()` çağrılır. Exception 502'ye çevrilir.

### `GET /api/v5/motor-logs`

Session ID ile `MotorRunLogStore.GetLatest()` çağrılır. Kayıt yoksa:

```json
{ "available": false }
```

Kayıt varsa:

```json
{ "available": true, "log": { ... } }
```

Buradaki `log` yapısının ayrıntıları `MotorRunLogStore` gerçek implementasyonuna göre değişir; bu doküman alan uydurmaz.

## 8. Deploy endpoint'leri

### `GET /api/deploy/log`

`/app/deploy.log` dosyası varsa son 150 satırı döndürür:

```json
{
  "lines": ["..."],
  "updated": true
}
```

Dosya yoksa boş liste ve `updated:false` döner.

### `POST /api/deploy/manual`

Önce CHPP bağlantısı kontrol edilir. Bağlı değilse 401. `GITHUB_ACTIONS_TOKEN` yoksa 503.

Başarılı çağrıda GitHub Actions `v5-build.yml` workflow'u `v5` ref'i ile `workflow_dispatch` edilir. Başarısız GitHub cevabı 502, cancellation 499 olarak döndürülür.

Bu endpoint deploy'un tamamlandığını değil, workflow dispatch isteğinin başarılı olduğunu garanti eder.

## 9. Production analysis data flow

`AnalysisService.RunAsync()` gerçek çağrı sırasını şu şekilde yürütür:

1. `teamdetails` v3.0 — kullanıcı takım ID/name.
2. `training` v1.1 — self confidence.
3. `players` v1.3 — kullanıcı oyuncuları.
4. `matches` v2.2 — seçilmiş gelecek lig maçı.
5. Rakip `matches` v2.2 — rakibin son resmi/rekabetçi geçmiş maçı.
6. Rakibin `matchlineup` v1.1 — geçmiş maçın saha 11'i.
7. Rakibin `matchdetails` v1.4 — geçmiş maç regional rating değerleri.
8. Rakip `players` v1.3 — rakip oyuncu listesi.
9. `MatchDataContext` oluşturulur.
10. `MotorPipelineService.RunAsync()` çalışır.
11. Final plan rating'i self-confidence ile `ConfidenceRatingAdjuster.Apply()` üzerinden düzeltilir.
12. `Analysis` response'u oluşturulur.

Seçilen maç cookie'deki `v5.matchId` değerinden okunur. Maç ID pozitif integer olmalı, tarih gelecekte olmalı ve `MatchType == 1` olmalıdır.

## 10. Historical opponent reconstruction

Rakibin geçmiş kadrosu doğrudan güncel oyuncu rating'i gibi kabul edilmez.

`SelectFinalFieldPlayers()`:

- `Lineup` oyuncularını okur.
- `StartingLineup` içindeki role 1–11 oyuncuları başlangıç saha kümesi olarak alır.
- substitutions içindeki `SubjectPlayerID -> ObjectPlayerID` değişimlerini uygular.
- başlangıç kümesi boşsa final lineup role 1–11 oyuncularına fallback yapar.
- `PositionCode > 0` oyuncularını tutar.
- PlayerID'ye göre tekilleştirir.

Her historical slot için yıldız değeri parse edilir ve `OpponentRatingEstimator.Estimate()` ile role-specific RP üretilir. Bu değer mevcut gerçek oyuncu skill'leri değildir; geçmiş maçta görülen yıldızlardan türetilen historical slot rating'idir.

## 11. RatingContext ve mevcut taktik sınırı

Production `AnalysisService`, `RatingContext` oluştururken:

```text
TeamTactic.Normal
```

verir.

Bu nedenle mevcut production path'te ayrı bir tactical selector bulunmuyor. `AdvancedTacticalScenarioEngine` ve `M8ChanceAllocationEngine` verilen taktiğin sonuçlarını hesaplar; kendileri taktik seçmez. `M10FinalDecisionEngine` ise `TeamAttitude` yaklaşımını seçebilir; bu `TeamTactic` ile aynı kavram değildir.

Developer tarafında bu ayrım korunmalıdır. UI/API response'una hesaplanmış final taktik gibi `AttackMiddle` veya `AttackWings` yazmak mevcut kod tarafından desteklenmez.

## 12. HTTP hata davranışları

Kodda açıkça görülen ana durumlar:

| Durum | Response |
|---|---|
| CHPP bağlı değil | 401 Unauthorized |
| Questionnaire enum geçersiz | 400 Bad Request |
| Secret eksik (`/api/deploy/manual`) | 503 |
| Analysis exception | 502 |
| Offline export unauthorized | 401 |
| Offline export diğer exception | 502 |
| Reference match exception | 502 |
| GitHub dispatch başarısız | 502 |
| Deploy request cancellation | 499 |

Authentication redirect endpoint'leri exception durumlarını root URL'ye `error` query parametresiyle yönlendirebilir.

## 13. JSON / modelleme notları

ASP.NET HTTP JSON serializer camelCase policy ile yapılandırılmıştır. C# model property'leri JSON'da camelCase olarak görünür.

Örneğin C# tarafındaki `MatchQuestionnaire.MatchImportance`, HTTP JSON katmanında `matchImportance` olarak taşınır.

Motor response'larının ayrıntılı şeması tek tek DTO'lar üzerinden koddan takip edilmelidir; bu belge yalnızca endpoint'in gerçekten expose ettiği ana nesneleri tanımlar.

## 14. Developer test noktaları

Bir production endpoint testi için minimum kontrol sırası:

1. `/health` erişilebilir mi?
2. `/api/v5/build` beklenen build'i veriyor mu?
3. `/api/v5/status` session bağlantı durumunu doğru veriyor mu?
4. CHPP login sonrası `/api/v5/status.connected == true` mı?
5. Questionnaire POST geçerli enum'ları kabul ediyor mu?
6. Analysis çağrısı bağlı session ile başlıyor mu?
7. Analysis başarısında motor run log kapanıyor mu?
8. Analysis exception'ı 502 ve failed motor log ile sonuçlanıyor mu?
9. Historical opponent lineup tam 11 oyuncuya indirgeniyor mu?
10. Response'ta final plan, M7/M7.2/M8/M9/M10 ve pipeline alanları gerçekten mevcut mu?

## 15. Source map

| Konu | Gerçek kaynak |
|---|---|
| HTTP app / endpoints / session | `HattrickAI_V5/Program.cs` |
| CHPP OAuth + XML client | `HattrickAI_V5/Core/ChppV5.cs` |
| Production analysis orchestration | `HattrickAI_V5/Core/AnalysisService.cs` |
| Motor orchestration | `HattrickAI_V5/Core/MotorPipelineService.cs` |
| Frontend API tüketimi | `HattrickAI_V5/wwwroot/index.html`, `HattrickAI_V5/wwwroot/motor-render.js` |
| Motor technical details | `HattrickAI_V5/Docs/MOTOR_TECHNICAL_MANUAL.md` |
| Data structures | `HattrickAI_V5/Docs/DATA_MODEL.md` |
| System flow | `HattrickAI_V5/Docs/SYSTEM_ARCHITECTURE.md` |

## 16. Bilinçli olarak kapsam dışı bırakılanlar

Aşağıdakiler kodda bu belge için yeterli doğrulukta görülmediği için varsayılarak yazılmamıştır:

- CHPP provider'ın dışarıdaki OAuth ekranının iç davranışı.
- Production API için OpenAPI/Swagger sözleşmesi; repository'de böyle bir sözleşme bu aşamada doğrulanmadı.
- API response'larının gelecekteki sürümler için stabil schema garantisi.
- Motorların üretmediği bir tactical selector.
- Fixture'da bulunmayan M8/M9/M10/M11 gerçek maç sonucu değerleri.
- Deploy workflow'unun tamamlanma sonucu; endpoint yalnızca dispatch eder.

## 17. Stage 7 sonucu

Developer/API manuelinin amacı, mevcut V5'in web sınırını ve backend çağrı zincirini kod seviyesinde okunabilir hale getirmektir. Bir sonraki dokümantasyon adımında bu belge, önceki Stage 1–6 belgeleriyle birleştirilerek PDF manuelinin developer bölümünün kaynağı olarak kullanılabilir.
