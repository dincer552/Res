# HattrickAI V5 — Aşama 6: Web Kullanıcı Manueli

## 1. Amaç

Bu belge HattrickAI V5 web arayüzünün mevcut repository koduna göre nasıl kullanılacağını açıklar.

Belge yalnızca mevcut frontend kodunda doğrulanabilen davranışları anlatır. Motorların üretmediği bir sonuç kullanıcıya vaat edilmez.

---

## 2. Web ekranının ana bölümleri

Mevcut `HattrickAI_V5/wwwroot/index.html` ekranında temel olarak şu bölümler bulunur:

1. Üst bilgi çubuğu
2. CHPP bağlantı durumu
3. Maç analizi bölümü
4. Kullanıcı takımı / önerilen XI sahası
5. Rakip takım / son bilinen yerleşim alanı
6. V5 runtime durum satırı

Üst çubukta:

- `HattrickAI` marka başlığı,
- build bilgisi,
- `CHPP bağlan` düğmesi bulunur.

CHPP durum alanı başlangıçta bağlantının kontrol edildiğini belirtir.

---

## 3. CHPP bağlantısı

Analiz düğmesi başlangıçta disabled durumundadır. Ekran, kullanıcı hesabının CHPP bağlantısını kurmasını bekler.

Kullanıcı akışı:

```text
Web ekranı
   ↓
CHPP bağlantısını kontrol et / bağlan
   ↓
Analiz düğmesi aktif hale gelir
   ↓
ANALİZİ ÇALIŞTIR
```

Bağlantı kurulmadan analiz butonunun aktif olması frontend sözleşmesinde beklenen davranış değildir.

---

## 4. Analizi başlatma

`ANALİZİ ÇALIŞTIR` düğmesi analiz akışını başlatır.

Frontend kullanıcıdan üç kısa maç koşulu ister.

### Soru 1 — Teknik direktör tarzı

Mevcut seçenekler:

- Dengeli (`Neutral`)
- Hücum (`Offensive`)
- Defans (`Defensive`)

### Soru 2 — Takım ruhu

Mevcut seçenekler Hattrick takım ruhu seviyelerini kapsar:

- `Murderous`
- `Furious`
- `Irritated`
- `Composed`
- `Calm`
- `Content`
- `Satisfied`
- `Delirious`
- `WalkingOnClouds`
- `ParadiseOnEarth`

Arayüzde bunların Türkçe karşılıkları gösterilir.

### Soru 3 — Maç önemi / yaklaşımı

Frontend üç soru tamamlanmadan `DEVAM ET` düğmesini aktif etmez.

Ekran ayrıca diğer maç koşullarının V5 tarafından nötr varsayımlarla tamamlandığını belirtir.

---

## 5. Analiz sırasında ekran

Analiz devam ederken kullanıcıya:

`CHPP verileri okunuyor ve bölgesel rating hesaplanıyor…`

mesajı gösterilir.

Bu mesaj frontend durum bilgisidir. Tek başına belirli bir motor sonucunun üretildiği anlamına gelmez.

Hata oluşursa hata alanı açılır ve frontend tarafından alınan hata mesajı gösterilir.

---

## 6. Önerilen XI

Kullanıcı takım kartı `KULLANICI TAKIMI` başlığıyla gösterilir.

Analiz tamamlandığında:

- önerilen 11 oyuncu,
- oyuncuların pozisyonları,
- oyuncu rating bilgileri,
- önerilen diziliş,
- bölgesel rating panosu

görsel saha üzerinde gösterilebilir.

Saha slotları V5 pozisyon sözleşmesini kullanır:

```text
GK
DEF-L / DEF-CL / DEF-C / DEF-CR / DEF-R
W-L / IM-L / IM-C / IM-R / W-R
FW-L / FW-C / FW-R
```

Bir oyuncu ilgili slotta varsa kartta oyuncu adı ve rating bilgisi gösterilir.

---

## 7. Oyuncu rating gösterimi

`motor-render.js` mevcut frontend render katmanında oyuncu kartlarında:

- kullanıcı takımında `RP=<rating>` formatını,
- rakip tarafında geçerli historical stars varsa `SP=<stars>` formatını

gösterebilir.

Bu değerlerin sunum biçimi frontend'e aittir; motor hesaplamasının kendisi değildir.

---

## 8. Oyuncu davranış emirleri

Frontend `motor-render.js` içinde pozisyon davranış emirlerini şu şekilde etiketler:

```text
0 = NORMAL
1 = OFANSİF
2 = DEFANSİF
3 = MERKEZE
4 = KANA
```

Bu alan oyuncunun davranış emrinin UI gösterimidir.

Bunlar takım taktiği ile aynı kavram değildir.

Örneğin bir oyuncunun `OFANSİF` emri gösterilmesi, takımın `AttackWings` veya `AttackMiddle` taktiği seçtiği anlamına gelmez.

---

## 9. Bölgesel rating panosu

Saha üzerinde yedi bölgesel değer gösterilir:

```text
DEF-L   DEF-C   DEF-R
          MID
ATT-L   ATT-C   ATT-R
```

`motor-render.js` bu değerleri sırasıyla:

```text
leftDefence
centralDefence
rightDefence
midfield
leftAttack
centralAttack
rightAttack
```

alanlarından alır.

Bu alanlar takımın bölgesel rating görünümüdür.

---

## 10. Rakip kartı

Rakip bölümü `RAKİP TAKIMI` olarak gösterilir.

Mevcut frontend açıklamasına göre burada rakibin son maçındaki gerçek yerleşim ve bölgesel ratingleri kullanılabilir.

Gerçek maç referansı varsa ayrı bir `BAZ ALINAN MAÇ` alanı gösterilebilecek şekilde HTML yapısı mevcuttur.

Aşama 5 fixture örneğinde bu yaklaşım rakibin geçmiş CHPP maçının gelecek maçtan ayrı tutulmasıyla doğrulanmıştır.

---

## 11. Diziliş gösterimi

V5'in gerçek motor zincirinde diziliş M4/M5/M10/M11 akışının bir parçasıdır.

Web tarafında önerilen diziliş `ownFormation` alanında gösterilir.

Örnek gerçek fixture:

```text
S4MSUNFC
3-5-2
```

Bu değer Stage 5 gerçek fixture analizinde doğrulanmıştır.

---

## 12. Taktik konusu — kullanıcı açısından kritik not

Mevcut production web analiz yolunda bağımsız takım taktiği seçen bir selector bulunmamaktadır.

Kod akışı:

```text
AnalysisService
      ↓
TeamTactic.Normal
      ↓
MotorPipelineService
      ↓
M7 / M7.2 / M8
```

M7.2 verilen taktiğin etkilerini hesaplar.

M8 verilen taktiğe göre şans/dönüşüm etkilerini hesaplar.

M10 ise `TeamAttitude` seçebilir; bu `TeamTactic` değildir.

Bu nedenle kullanıcı arayüzü:

```text
ORTADAN ATAK
KANATTAN ATAK
KONTRA ATAK
```

değerlerinden birini motorun "hesapladığı en iyi taktik" olarak göstermemelidir.

Mevcut web production path için doğru semantik:

```text
TAKTİK YOK
```

Altındaki gerçek input:

```text
TeamTactic.Normal
```

---

## 13. Analiz sonucunu yorumlama

Kullanıcı aşağıdaki üç şeyi birbirinden ayırmalıdır:

### A — Diziliş

Örneğin `3-5-2`.

Bu, XI'nin pozisyon yapısıdır.

### B — Oyuncu davranış emri

Örneğin `OFANSİF`, `DEFANSİF`, `MERKEZE`, `KANA`.

Bu, oyuncu bazındaki davranış emridir.

### C — Takım taktiği

`Normal`, `AttackMiddle`, `AttackWings`, `CounterAttack` gibi kavramlar takım taktiğidir.

Mevcut production web path'te C kategorisini seçen bağımsız bir selector bulunmamaktadır.

Bu ayrım yanlış kullanıcı yorumlarını önlemek için özellikle korunmalıdır.

---

## 14. Gerçek maç örneği

Aşama 5'te kullanılan fixture'da gelecek maç:

```text
769648177
Zeytinburnu Sahil Spor - S4MSUNFC
06.09.2026 15:00 UTC
```

S4MSUNFC için fixture'da saklanan V5 analizi:

```text
Formation: 3-5-2
Midfield: 6.25
Total Defence: 35.50
Total Attack: 31.00
```

Rakip:

```text
Formation: 2-5-3
Midfield: 7.00
Total Defence: 22.00
Total Attack: 31.50
```

Bu sayılar web ekranında kullanılan/oluşturulan gerçek fixture verisinin örneğidir; her analizde aynı değerlerin çıkacağı anlamına gelmez.

---

## 15. Kullanıcı için kısa kullanım akışı

```text
1. HattrickAI V5 web ekranını aç
        ↓
2. CHPP bağlantısını kontrol et / bağlan
        ↓
3. ANALİZİ ÇALIŞTIR
        ↓
4. Teknik direktör tarzını seç
        ↓
5. Takım ruhunu seç
        ↓
6. Maç önemi / yaklaşımı sorusunu cevapla
        ↓
7. DEVAM ET
        ↓
8. CHPP verilerinin okunmasını bekle
        ↓
9. Önerilen XI ve bölgesel ratingleri incele
        ↓
10. Rakibin son bilinen kadro/ratinglerini karşılaştır
```

---

## 16. Mevcut arayüzde dikkat edilmesi gerekenler

- Analiz butonunun disabled olması bağlantı durumuyla ilişkilidir.
- Analiz sırasında bekleme mesajı gösterilir.
- Hata durumunda frontend hata alanını gösterir.
- Oyuncu adı, rating ve davranış emri saha üzerinde render edilir.
- Bölgesel ratingler saha üzerindeki rating board'da gösterilir.
- Oyuncu davranış emirleri takım taktiği değildir.
- Diziliş ile takım taktiği aynı kavram değildir.
- Mevcut production path'te "en iyi takım taktiği" seçildiği iddia edilmemelidir.

---

## 17. Teknik kaynaklar

Web kullanıcı davranışının ana kaynakları:

- `HattrickAI_V5/wwwroot/index.html`
- `HattrickAI_V5/wwwroot/motor-render.js`
- `HattrickAI_V5/Core/AnalysisService.cs`
- `HattrickAI_V5/Core/MotorPipelineService.cs`
- `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs`
- `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs`
- `HattrickAI_V5/Core/M10FinalDecisionEngine.cs`

Motorların teknik hesaplama ayrıntıları için:

`HattrickAI_V5/Docs/MOTOR_TECHNICAL_MANUAL.md`

Gerçek maç örneği için:

`HattrickAI_V5/Docs/REAL_MATCH_ANALYSIS.md`

---

## 18. Aşama 6 sınırı

Bu belge mevcut web kullanıcı deneyimini açıklar. Burada olmayan özellikler mevcut kabul edilmez.

Özellikle:

- bağımsız takım taktiği selectorü,
- frontend'in kendi başına maç sonucu tahmini,
- motorlarda bulunmayan ekstra kullanıcı seçenekleri,
- fixture'da bulunmayan sonuçların frontend tarafından üretildiği iddiası

dokümana dahil edilmemiştir.

Aşama 6 tamamlanmıştır.
