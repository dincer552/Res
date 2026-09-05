# HattrickAI V5 — Web UI File Map

**Amaç:** `wwwroot` içindeki dosyaların ekranda neyi oluşturduğunu, hangi dosyanın hangi işi yaptığını ve ana analiz ekranıyla ilişkisini tek yerde göstermek.

## 1. Önemli ayrım

Ekran görüntüsündeki dosyaların tamamı aynı klasörde olsa da **hepsi aynı HTML ekranı değildir**.

`HattrickAI_V5/wwwroot/` klasörü üç tip kaynak içeriyor:

1. **Ana analiz sayfası:** `index.html`
2. **Ana sayfaya sonradan davranış/panel ekleyen JavaScript'ler:** `*.js`
3. **Ayrı açılan yardımcı sayfalar:** `deploy.html`, `historical-export.html`

Dolayısıyla önceki `WEB_INTERFACE.md` belgesi ana web arayüzünü anlatıyordu; bu belge ise kullanıcının ekran görüntüsündeki **dosya → ekran → görev** ilişkisini anlatır.

## 2. Ana ekranın gerçek sahibi

### `index.html`

**Dosya:** `HattrickAI_V5/wwwroot/index.html`

Bu ana web sayfasının temel HTML/CSS gövdesidir.

Burada doğrudan tanımlanan ana UI:

- HattrickAI üst barı
- build alanı
- CHPP bağlan butonu
- bağlantı durum kartı
- hata alanı
- analiz butonu
- soru kartının temel HTML'i
- kullanıcı takım kartı
- rakip takım kartı
- saha/pitch container'ları
- rating alanının container'ları
- runtime alanı

Ayrıca temel CSS de aynı dosyadadır.

Kaynak dosyanın mevcut SHA'sı: `30879fe804726efa9314bb2d2789cf12280a6d64`.

## 3. Ana ekranın çalışma mantığı nasıl parçalanmış?

Kabaca gerçek yapı:

```text
                         wwwroot/index.html
                                |
              +-----------------+------------------+
              |                 |                  |
              v                 v                  v
        temel HTML/CSS     seçim/maç akışı     render/paneller
              |                 |                  |
              |                 v                  +--> motor-render.js
              |          match-select.js           +--> motor-logs.js
              |                                    +--> formation-competition.js
              |                                    +--> m9-prediction.js
              |                                    +--> m9-prediction-bridge.js
              |                                    +--> copy-teams.js
              |                                    +--> team-header.js
              |                                    +--> calibration.js
              |
              +---------------------> /api/v5/*
                                         |
                                         v
                                  ASP.NET Core / Program.cs
                                         |
                                         v
                                  AnalysisService
                                         |
                                         v
                                  MotorPipelineService
```

Not: JavaScript dosyalarının bir kısmı DOM'a sonradan panel ekliyor. Özellikle `motor-render.js` kendi `<style>` ve `team-header.js` yüklemesini de yapıyor.

## 4. Dosya dosya görev dağılımı

| Dosya | Tip | Ana görevi | Ana ekranla ilişkisi |
|---|---|---|---|
| `index.html` | HTML | Ana ekranın temel gövdesi ve CSS'i | **Ana ekran** |
| `match-select.js` | JS | Maç seçimi + 3 yerine 4 adımlı seçim akışı | Ana ekranı değiştirir |
| `motor-render.js` | JS | Saha, oyuncu slotları, rating board ve order render'ı | Ana sonuç ekranının çekirdeği |
| `team-header.js` | JS | Takım adı, logo, ev/deplasman başlığını düzenler | Ana takım kartlarını değiştirir |
| `motor-logs.js` | JS | M3→M11 motor paneli, ilerleme ve diagnostics | Ana ekrana motor paneli ekler |
| `formation-competition.js` | JS | M10 formation competition paneli | Ana ekrana panel ekler |
| `m9-prediction.js` | JS | M9 maç tahmini paneli ve ayrıntıları | Ana ekrana panel ekler |
| `m9-prediction-bridge.js` | JS | M9 panelini analysis response/event akışına bağlar | Ana ekrandaki M9 güncellemesini sağlar |
| `copy-teams.js` | JS | İki takımın bilgilerini clipboard'a kopyalar | Ana ekrandaki copy davranışını değiştirir |
| `calibration.js` | JS | Tarihsel production corpus/test dataset üretimi ve JSON export yardımcıları | UI yardımcı/kalibrasyon tarafı |
| `deploy.html` | HTML | Canlı deploy log ekranı | **Ayrı sayfa** |
| `historical-export.html` | HTML | 300 maçlık tarihsel CHPP corpus toplama ekranı | **Ayrı sayfa** |

## 5. `match-select.js` — kullanıcı aslında neden 4 soru görüyor?

Burada önemli bir kaynak kod ayrımı var.

`index.html` içinde başlangıçta `QUESTIONS` dizisi 3 maç koşulu sorusu olarak tanımlanmış görünüyor.

Ancak `match-select.js` kendi `FOUR_QUESTIONS` dizisini tanımlıyor ve akışı değiştiriyor:

1. Hangi lig maçını analiz etmek istiyorsun?
2. Teknik direktör tarzın nasıl?
3. Takım ruhu hangi seviyede?
4. Bu maçta hangi yaklaşımı kullanıyorsun?

Maç seçimi:

- `/api/v5/reference-match` çağrısıyla yaklaşan lig maçlarını alır.
- seçilen `matchId` değerini `v5.matchId` cookie'sine yazar.
- cookie süresi 8 saattir.
- son adımda `startAnalysis()` çağrılır.

Bu nedenle **gerçek çalışan arayüzü dokümante ederken 4 adımlı akış esas alınmalıdır**. Eski `index.html` içindeki 3 soruluk statik şablon tek başına güncel UI akışını temsil etmiyor.

## 6. `motor-render.js` — sahadaki oyuncular nereden geliyor?

Bu dosya:

- `GK`
- `DEF-L / DEF-CL / DEF-C / DEF-CR / DEF-R`
- `W-L / IM-L / IM-C / IM-R / W-R`
- `FW-L / FW-C / FW-R`

slotlarını tanımlar.

`window.makePitch(target, lineup, rating)` fonksiyonu ile saha oluşturulur.

Oyuncu kartında:

- oyuncu adı
- RP veya rakip için geçerli historical stars varsa SP
- order etiketi

gösterilir.

Order eşlemesi:

```text
0 -> NORMAL
1 -> OFANSİF
2 -> DEFANSİF
3 -> MERKEZE
4 -> KANA
```

Ayrıca 7 bölgesel rating'i saha üzerindeki rating board'a basar:

```text
DEF-L  DEF-C  DEF-R
       MID
ATT-L  ATT-C  ATT-R
```

## 7. `team-header.js` — takım adı ve logo neden ayrı dosyada?

Bu dosya ana takım kartının header'ını sonradan düzenler.

Şunları yapar:

- takım adını günceller
- ev sahibi/deplasman rolünü gösterir
- takım logosunu `/api/v5/reference-match` sonucundan alır
- eski shield görüntüsünü logo ile değiştirir
- kartın gereksiz subtitle/copy alanlarını gizler

Seçilen maç değişince `options` click event'i üzerinden tekrar yüklenir.

Yani takım başlığının son hali yalnızca `index.html`'deki statik HTML değildir.

## 8. `motor-logs.js` — M3 → M11 paneli

Bu dosya ana sayfaya:

`🧠 V5 Motor Paneli • M3 → M11`

panelini ekler.

Görevleri:

- motorların durumunu göstermek
- analiz ilerleme çubuğu oluşturmak
- motor run log'unu `/api/v5/motor-logs` üzerinden okumak
- JSON sonuç export'u sunmak
- M9 diagnostics göstermek
- Monte Carlo ve event→goal tanı bilgilerini göstermek

Motor listesi kodda:

```text
M3
M4
M5
M6
M7
M7.2
M8
M9
M10
M6-B
M11
```

## 9. `formation-competition.js` — M10 paneli

Ana sayfaya `🏆 Formation Competition` panelini ekler.

Veriyi doğrudan motor hesabından yeniden üretmez. Başarılı `/api/v5/analysis` response'undan:

`m10Decision.formationCompetition`

veya

`motorPipeline.m10.formationCompetition`

alanını okur.

Gösterdiği ana alanlar:

- rank
- formation
- candidate count
- composite score
- tactical score
- win probability
- search depth status
- margin vs next

Panel varsayılan olarak açık gelir ve aç/kapa yapılabilir.

## 10. `m9-prediction.js` — M9 kullanıcı paneli

Bu dosya ana sayfaya:

`🎯 M9 Maç Tahmini`

panelini ekler.

Response içinden M9 prediction nesnesini arar ve gösterir:

- tahmin sonucu
- analitik en olası skor
- güven etiketi
- galibiyet / beraberlik / rakip olasılıkları
- beklenen goller
- topa sahip olma
- 7 rating/pozisyon eşleşmesi
- Event → Goal motoru
- event katkıları
- Monte Carlo sonuçları
- senaryo dağılımları
- calibration status

Bu panel frontend'in M9 hesabını kendisinin yaptığı anlamına gelmez. Backend tarafından dönen sonucu render eder.

## 11. `m9-prediction-bridge.js` — neden iki M9 dosyası var?

`m9-prediction.js` ve `m9-prediction-bridge.js` aynı kavramı iki farklı frontend katmanında ele alıyor.

Bridge dosyası:

- `v5:analysis-ready` event'ini dinler.
- `/api/v5/analysis` response'unu izlemek için `window.fetch` wrapper kullanır.
- M9 response'u geldiğinde M9 panelini günceller.

Bu nedenle bridge'in görevi **M9 hesabı yapmak değil, analysis response → M9 UI bağlantısını kurmaktır**.

## 12. `copy-teams.js` — Kopyala butonu

Bu dosya kullanıcı ve rakip takım bilgilerinin birlikte clipboard'a kopyalanmasını sağlar.

Güncel davranışta:

- rakip ayrı copy butonu kaldırılır.
- kullanıcı kartındaki buton `İKİ TAKIMI KOPYALA` haline getirilir.
- takım adı
- diziliş
- oyuncular
- RP/SP
- oyuncu davranışları
- 7 bölgesel rating

tek metin halinde kopyalanır.

## 13. `calibration.js` — ana analiz ekranı mı?

Hayır.

Bu dosyanın amacı tarihsel production verisini test/kalibrasyon corpusuna dönüştürmek ve JSON indirme yardımcıları sağlamaktır.

Kodda:

- CHPP kaynaklı satırlar filtrelenir.
- gözlenen sector chance değerleri sample'a dönüştürülür.
- `hattrickai-v5-historical-production-v1` schema adı kullanılır.
- `minimumAcceptanceMatches: 250` metadata olarak yazılır.
- JSON dosyası browser tarafından indirilir.

Bu nedenle ana kullanıcı analiz sonucunun hesap motoru değildir.

## 14. `historical-export.html` — ayrı tarihsel veri ekranı

Bu dosya ana `/` ekranının parçası değildir.

Kendi standalone HTML sayfasıdır.

Kullanıcıya:

`300 MAÇLIK TARİHSEL CORPUSU TOPLA`

butonunu verir.

Buton:

`GET /api/v5/offline-export?historical=1`

çağrısını yapar.

Sonuç JSON olarak indirilir.

Sayfa ayrıca CHPP isteklerinin tek tek ve yaklaşık 1 saniye aralıkla yapıldığını kullanıcıya bildirir.

## 15. `deploy.html` — ayrı deploy ekranı

Bu da ana analiz ekranından ayrı bir HTML sayfasıdır.

Başlık:

`HattrickAI • Deploy Log`

Görev:

`/api/deploy/log`

endpoint'ini yaklaşık 2 saniyede bir polling ederek deploy loglarını gösterir.

Log satırlarını:

- başarılı
- hata
- bilgi

olarak renklendirir ve son deploy başarısızsa ayrı hata kutusu gösterir.

## 16. Backend bağlantısı

Frontend dosyaları motor hesaplarını browser'da baştan uygulamaz.

Ana veri akışı:

```text
wwwroot JS
   |
   +--> /api/v5/status
   +--> /api/v5/build
   +--> /api/v5/reference-match
   +--> /api/v5/questionnaire
   +--> /api/v5/analysis
   +--> /api/v5/motor-logs
   +--> /api/v5/offline-export
   +--> /api/deploy/log
          |
          v
     Program.cs
          |
          v
     AnalysisService
          |
          v
   MotorPipelineService
          |
          v
       M3→M11
```

## 17. Ekranda görülen panelin hangi dosyadan geldiğini hızlı bulma

| Ekrandaki bölüm | Kaynak |
|---|---|
| HattrickAI üst bar | `index.html` |
| CHPP bağlan | `index.html` + backend auth endpointleri |
| Maç seçimi | `match-select.js` |
| Teknik direktör / ruh / yaklaşım | `match-select.js` |
| Takım adı + logo | `team-header.js` |
| Yeşil saha | `index.html` CSS + `motor-render.js` |
| Oyuncular | `motor-render.js` |
| RP/SP | `motor-render.js` |
| 7 bölgesel rating | `motor-render.js` |
| M9 Maç Tahmini | `m9-prediction.js` + `m9-prediction-bridge.js` |
| Formation Competition | `formation-competition.js` |
| V5 Motor Paneli | `motor-logs.js` |
| İki takımı kopyala | `copy-teams.js` |
| Tarihsel corpus | `historical-export.html` + `calibration.js` |
| Deploy log | `deploy.html` |

## 18. Sonuç

Evet: **ekran görüntüsündeki klasör bizim web arayüzümüzün gerçek frontend kaynak klasörü.**

Ama `index.html` tek başına bütün ekran değildir. Ana ekranın önemli bölümleri sonradan çalışan JavaScript dosyaları tarafından değiştirilir veya eklenir.

Bu yüzden bundan sonraki UI değişikliklerinde yalnızca `index.html`'e bakmak yeterli değildir. Özellikle:

- seçim akışı için `match-select.js`,
- saha/oyuncu için `motor-render.js`,
- takım header için `team-header.js`,
- M9 için `m9-prediction*.js`,
- formation için `formation-competition.js`,
- motor paneli için `motor-logs.js`

birlikte kontrol edilmelidir.
