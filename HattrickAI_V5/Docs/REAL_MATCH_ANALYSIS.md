# HattrickAI V5 — Aşama 5: Gerçek Maç Örnek Analizi

## 1. Amaç

Bu belge Aşama 5 kapsamında repository içindeki gerçek CHPP offline fixture üzerinden V5 veri akışının somut bir maç üzerinde izlenmesini sağlar.

Kaynak fixture:

`TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json`

Fixture SHA: `540a63d381defbf49d4d89553370b90114bf4815`

Fixture export zamanı: `2026-09-01T08:49:55.1576611+00:00`

Kurallar:

- Yalnızca fixture içinde bulunan gerçek değerler kullanılır.
- Fixture'da bulunmayan M8/M9/M10/M11 çıktıları uydurulmaz.
- Tarihsel gerçek maç sonucu ile V5'in geleceğe dönük analiz girdisi birbirinden ayrı tutulur.

---

## 2. Analiz edilen gelecek maç

Fixture'daki `match.nextMatch`:

| Alan | Değer |
|---|---|
| Maç ID | `769648177` |
| Tarih | `2026-09-06 15:00 UTC` |
| Ev sahibi | Zeytinburnu Sahil Spor |
| Deplasman | S4MSUNFC |
| Maç tipi | `1` |
| OrdersGiven | `False` |

Bu nedenle örnek, S4MSUNFC'nin deplasman senaryosunu temsil eder.

Takımlar:

- S4MSUNFC
- Zeytinburnu Sahil Spor

Fixture'daki questionnaire:

```text
Coach = 0
TeamSpirit = 3
MatchImportance = 0
```

---

## 3. Rakibin kullanılan referans maçı

V5 fixture içinde rakibin son referans maçı olarak:

```text
MatchID: 769648173
2026-08-30 15:00 UTC
bombacı mülayim spor 3 - 2 Zeytinburnu Sahil Spor
```

Bu gerçek maçın CHPP `matchdetails.xml` verisi fixture içinde bulunmaktadır.

### Gerçek maçta kayıtlı takım ratingleri

| Rating | bombacı mülayim spor | Zeytinburnu Sahil Spor |
|---|---:|---:|
| Midfield | 31 | 28 |
| Left Defence | 29 | 25 |
| Central Defence | 32 | 38 |
| Right Defence | 31 | 25 |
| Left Attack | 23 | 40 |
| Central Attack | 31 | 52 |
| Right Attack | 17 | 34 |

Gerçek maç sonucu:

```text
bombacı mülayim spor 3
Zeytinburnu Sahil Spor 2
```

İlk yarı topa sahip olma:

```text
53% - 47%
```

İkinci yarı:

```text
50% - 50%
```

Goller 14, 30 ve 53. dakikalarda ev sahibi; 60 ve 89. dakikalarda Zeytinburnu Sahil Spor tarafından kaydedilmiştir.

Bu sonuç V5'in tahmini değildir; CHPP'den alınmış tarihsel maç verisidir.

---

## 4. Rakibin son bilinen kadrosu

Fixture'daki `normalized.opponentLastLineup` ve `v5Analysis.opponentLineup` verisine göre V5 rakibin son bilinen kadrosunu `2-5-3` olarak normalize etmiştir.

### Zeytinburnu Sahil Spor — 2-5-3

```text
GK      Ignacio Flores de Lizaur
DEF-CR  Sotear Dara
DEF-CL  Demirel Orhun
W-R     Yiğit Namdar
IM-R    Emre Baykam
IM-C    Sander Eser
IM-L    Ekrem Furkan Dilnur
W-L     Egemen Dinçer Saner
FW-R    Özgür Sinan Kenan
FW-C    Tommaso Falsone
FW-L    Ulisse Roepke
```

V5 fixture'daki normalize edilmiş ratingler:

```text
GK      19.94
DEF-CR  14.00
DEF-CL  14.00
W-R     11.99
IM-R    19.92
IM-C    20.00
IM-L    13.99
W-L     12.07
FW-R    20.00
FW-C    20.00
FW-L    20.00
```

---

## 5. S4MSUNFC oyuncu havuzu ve M3 sonucu

Fixture'da S4MSUNFC için normalize edilmiş oyuncu listesi ve `ownPlayerAnalysis` bulunmaktadır.

M3 gerçek çıktısından örnekler:

| Oyuncu | Primary | Primary score | Secondary | Secondary score |
|---|---|---:|---|---:|
| Enzo Bultot | GK | 17.90 | FW-C | 4.64 |
| Abeiku Takyi | DEF-CL | 19.17 | DEF-C | 19.17 |
| Adrian Beţa | FW-C | 17.55 | FW-L | 16.74 |
| Bertalan Doktor | IM-C | 21.64 | IM-L | 20.47 |
| Cristian Pesalovo | DEF-CL | 18.39 | DEF-C | 18.39 |
| Felix Gustavsson | W-L | 19.00 | W-R | 19.00 |
| Manuel Gobiet | W-L | 17.64 | W-R | 17.64 |
| Milen Bozev | W-L | 16.94 | W-R | 16.94 |
| Nándor Dobóvári | W-L | 18.94 | W-R | 18.94 |
| Ersin Akşın | FW-C | 15.45 | FW-L | 14.82 |

Fixture'da `Biel Kichute` için `InjuryLevel=999` olduğundan eligibility kuralına göre oyuncu uygun değildir.

Bu, M3 eligibility kuralının gerçek fixture üzerindeki doğrudan örneğidir.

---

## 6. M4/M5 sonucu: V5'in oluşturduğu XI

Fixture'daki `v5Analysis.own.formation` değeri:

```text
3-5-2
```

V5 fixture'ında üretilmiş XI:

| Slot | Oyuncu | V5 rating |
|---|---|---:|
| GK | Enzo Bultot | 17.90 |
| DEF-CL | Abeiku Takyi | 19.17 |
| DEF-C | Dawid Nocoń | 18.72 |
| DEF-CR | Cristian Pesalovo | 18.39 |
| W-L | Felix Gustavsson | 19.00 |
| IM-L | Francisco Manuel | 17.48 |
| IM-C | Milen Bozev | 15.85 |
| IM-R | Bertalan Doktor | 20.47 |
| W-R | Nándor Dobóvári | 18.94 |
| FW-L | Adrian Beţa | 16.74 |
| FW-R | Ersin Akşın | 14.82 |

Bu tablo fixture'daki `v5Analysis.own.slots` verisinden alınmıştır.

---

## 7. M7 bölgesel rating sonucu

Fixture'daki `v5Analysis.ownRating` gerçek V5 rating çıktısını verir:

### S4MSUNFC

```text
Left Defence     9.75
Central Defence 16.00
Right Defence    9.75
Midfield         6.25
Left Attack     10.25
Central Attack  11.50
Right Attack     9.25

Total Defence   35.50
Total Attack    31.00
```

Raw değerler de fixture'da saklanmıştır:

```text
rawLeftDefence     8.814876331125825
rawCentralDefence 15.131275059602647
rawRightDefence    8.755167139072846
rawMidfield        5.3073582240775785
rawLeftAttack      9.3881423692354
rawCentralAttack  10.733139483443706
rawRightAttack     8.34554898438368
```

### Rakip

```text
Left Defence      6.25
Central Defence   9.50
Right Defence     6.25
Midfield          7.00
Left Attack      10.00
Central Attack   13.00
Right Attack      8.50

Total Defence    22.00
Total Attack     31.50
```

Bu aşamada doğrudan görülen karşılaştırma:

```text
S4MSUNFC midfield:       6.25
Rakip midfield:          7.00

S4MSUNFC total attack:  31.00
Rakip total attack:     31.50

S4MSUNFC total defence: 35.50
Rakip total defence:    22.00
```

Bunlar rating karşılaştırmalarıdır; tek başına maç sonucu olasılığı değildir.

---

## 8. Rakip tehdit modeli çıktısı

Fixture'daki `v5Analysis.opponentThreat` doğrudan şu değerleri içerir:

```text
Left threat       10.00
Centre threat     13.00
Right threat       8.50
Midfield pressure  7.00

Left defence barrier    6.25
Centre defence barrier  9.50
Right defence barrier   6.25

Max attack threat      13.00
Total attack threat    31.50
```

S4MSUNFC savunmasına yönelen tehdit:

```text
Our left defence    <- opponent right attack = 8.50
Our centre defence  <- opponent centre attack = 13.00
Our right defence   <- opponent left attack = 10.00
```

S4MSUNFC hücum fırsatı karşılıkları:

```text
Our left attack    -> opponent right defence = 6.25
Our centre attack  -> opponent centre defence = 9.50
Our right attack   -> opponent left defence = 6.25
```

Bu tablo V5 fixture'ındaki gerçek normalize edilmiş rakip ratinglerinden türetilmiş `opponentThreat` çıktısıdır.

---

## 9. Taktik durumu

Bu gerçek fixture, mevcut production path'te takım taktiğinin bağımsız olarak seçilmediği mimariyle uyumludur.

Fixture'ın `v5Analysis` bölümünde ayrı bir final tactical selector sonucu bulunmamaktadır.

Production kodunun doğrulanmış davranışı:

```text
AnalysisService -> TeamTactic.Normal
             ↓
MotorPipelineService
             ↓
M7 / M7.2 / M8 taktiği input olarak kullanır
```

Dolayısıyla bu maç için `AttackMiddle`, `AttackWings` veya `CounterAttack` seçildiği iddia edilmemelidir.

Bu örnekte taktik konusunda doğru dokümantasyon:

```text
Tactical selector: yok
Supplied TeamTactic: Normal
Semantic UI: TAKTİK YOK
```

---

## 10. Fixture'ın gösterdiği veri zinciri

```text
CHPP XML
   ↓
Offline JSON fixture
   ↓
Normalized players / opponent history
   ↓
M3 player analysis
   ↓
M4 formation feasibility
   ↓
M5 XI candidate
   ↓
M6 behaviour / tactical search
   ↓
M7 regional ratings
   ↓
M7.2 supplied-tactic scenario
   ↓
M8 chance model
   ↓
M9 prediction
   ↓
DB1
   ↓
M10
   ↓
M6-B
   ↓
DB2
   ↓
M11
```

Ancak bu fixture'ın `v5Analysis` kaydı, saklanan çıktı olarak M3/M4/M5 sonucunu ve M7 regional rating/opponent threat verisini açıkça içerirken M8/M9/M10/M6-B/DB2/M11 final sonuçlarının tamamını ayrı alanlar halinde saklamamaktadır.

Bu nedenle bu belge M8 sonrası değerleri fixture'da yokmuş gibi üretmez.

---

## 11. Tarihsel maç ile V5 analizi arasındaki ayrım

Aynı fixture içinde iki farklı zaman katmanı vardır:

### A — Gelecek maç

```text
06.09.2026
Zeytinburnu Sahil Spor - S4MSUNFC
```

Bu maç için V5 inputları ve normalize edilmiş aday analiz verileri vardır.

### B — Rakibin geçmiş maçı

```text
30.08.2026
bombacı mülayim spor 3 - 2 Zeytinburnu Sahil Spor
```

Bu maç tamamlanmıştır ve gerçek CHPP ratingleri/eventleri fixture'da bulunur.

Bu iki veri katmanı aynı şey değildir. Geçmiş maç sonucu gelecekteki V5 tahmini olarak yazılmamalıdır.

---

## 12. Aşama 5 sonucu

Aşama 5'in doğrulanmış çıktısı:

- Gerçek CHPP fixture seçildi.
- Gelecek maç ve rakibin geçmiş referans maçı ayrıştırıldı.
- M3 gerçek player-analysis sonuçlarından örnekler çıkarıldı.
- M4/M5 tarafından oluşturulan `3-5-2` XI fixture üzerinden gösterildi.
- M7 own/opponent regional ratingleri kaydedildi.
- Rakip threat/opportunity matrisi kaydedildi.
- Taktik selector olmadığı tekrar doğrulandı.
- Fixture'da bulunmayan M8/M9/M10/M11 final sonuçları uydurulmadı.

Bu belge gerçek maç örneğini teknik manuelin sonraki Web ve Developer/API bölümlerine bağlayan referans olarak kullanılacaktır.
