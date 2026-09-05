# HattrickAI V5 — Aşama 3: Hattrick Matematik Modeli

## 1. Amaç ve kaynak disiplini

Bu belge V5 repository içinde gerçek kodda bulunan maç matematiğini dokümante eder.

Kapsam:

- bölgesel rating hesapları,
- rating context etkileri,
- Team Spirit ve coach etkileri,
- possession hesabı,
- normal chance havuzu ve sektör dağılımı,
- taktik dönüşüm fonksiyonları,
- M7 -> M8 matematik bağlantısı.

Temel production dosyaları:

- `HattrickAI_V5/Core/RegionalRatingEngine.cs`
- `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs`
- `HattrickAI_V5/Core/QuestionnaireContext.cs`
- `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs`
- `HattrickAI_V5/Core/TacticPaperMappingEngine.cs`

Önemli ayrım: kodda "paper-derived" olarak işaretlenen katsayılar araştırma/reference mekanizmasından gelir. Bu belgede araştırma kaynağı ile V5'in gerçekten uyguladığı hesap ayrı tutulur.

---

## 2. Bölgesel rating modeli — M7 tabanı

### 2.1 Girdi

`RegionalRatingEngine.Calculate(...)` şu verileri kullanır:

- `IReadOnlyList<RegionalPlayer>`
- opsiyonel `RatingContext`

Her oyuncu için effective skill oluşturulur.

### 2.2 Form etkisi

Kodda form için bir `FormFactor` tablosu vardır ve oyuncunun form etkisi baseline'a göre normalize edilir:

```text
formMultiplier = FormFactor(Form) / 0.755
```

Kodun yorumu, araştırılmış coefficient table'ın standart form/stamina/experience uplift içerdiğini ve form/experience'in bunun üzerine ikinci kez stack edilmemesi gerektiğini belirtir.

### 2.3 Loyalty etkisi

Kod:

```text
LoyaltyEffect(loyalty) =
    0                         loyalty <= 0
    clamp(loyalty * 0.05, 0, 1)   loyalty > 0
```

### 2.4 Experience etkisi

`ExperienceBonus` sabit lookup tablosu kullanır. Kodda 1–20 aralığı clamp edilir ve experience değeri yuvarlanarak tablo index'ine çevrilir.

Tablonun başlangıç ve üst örnekleri:

```text
index 1 -> 0.00
index 2 -> 0.40
index 3 -> 0.64
index 4 -> 0.80
index 5 -> 0.93
index 6 -> 1.04
index 7 -> 1.13
...
index 20 -> 1.73
```

Effective skill içinde experience baseline değeri `1.13` çıkartılır; loyalty ve experience delta temel skill değerlerine eklenir.

```text
effective skill = base skill + loyalty + (ExperienceBonus - 1.13)
```

Form ise ayrı `formMultiplier` olarak ilgili contribution'a uygulanır.

---

## 3. Pozisyon contribution katsayıları

`RegionalRatingEngine.AddPositionContribution(...)` oyuncu pozisyonuna göre yedi rating sektörüne katkı ekler:

```text
Left Defence
Central Defence
Right Defence
Midfield
Left Attack
Central Attack
Right Attack
```

### 3.1 Goalkeeper

Central Defence:

```text
Keeper * 0.165 + Defending * 0.079
```

Sol ve sağ defence toplamına ayrı ayrı:

```text
Keeper * 0.183 + Defending * 0.082
```

Bu katkılar form multiplier ile çarpılır.

### 3.2 Central Defender

Merkez defence katsayısı order'a göre:

```text
Normal     Defending * 0.186
Offensive  Defending * 0.130
TowardsWing Defending * 0.133
```

Yan defence:

```text
Normal       Defending * 0.077
Offensive    Defending * 0.058
TowardsWing  Defending * 0.217
```

Midfield:

```text
Normal       Playmaking * 0.035
Offensive    Playmaking * 0.047
TowardsWing  Playmaking * 0.023
```

Midfield contribution ayrıca central-defender count penalty ile çarpılır:

```text
2 central defender  -> 0.964
3+                  -> 0.900
otherwise           -> 1.000
```

Central defender `TowardsWing` ve center dışında ise passing ile ilgili side attack contribution da eklenir:

```text
Passing * 0.063
```

### 3.3 Wing Back

Order'a göre katsayılar:

| Order | Central Def | Side Def | Midfield | Side Attack |
|---|---:|---:|---:|---:|
| Defensive | 0.089 | 0.284 | 0.009 | 0.082 |
| TowardsMiddle | 0.126 | 0.209 | 0.023 | 0.072 |
| Offensive | 0.071 | 0.175 | 0.032 | 0.163 |
| Normal | 0.083 | 0.268 | 0.023 | 0.129 |

Central ve side defence `Defending`, midfield `Playmaking`, side attack `Winger` üzerinden hesaplanır.

### 3.4 Inner Midfielder

Order'a göre `OrderMatrix`:

```text
Defensive:
CentralDefence = 0.115
SideDefence    = 0.040
Midfield       = 0.131
SidePassing    = 0.018
CenterPassing  = 0.039
CenterScoring  = 0.028
SideWinger     = 0

Offensive:
0.115, 0.040, 0.131, 0.018, 0.039, 0.025, 0

TowardsWing:
0.059, 0.068, 0.113, 0.064, 0.038, 0, 0.117

Normal:
0.070, 0.028, 0.139, 0.028, 0.057, 0.038, 0
```

Midfield ayrıca inner-midfielder count penalty alır:

```text
2 inner midfielders -> 0.935
3+                 -> 0.825
otherwise           -> 1.000
```

Passing katkısı center oyuncuda iki wing attack sektörüne birlikte, side oyuncuda kendi side'ına uygulanır.

### 3.5 Winger

Order'a göre `WingerMatrix`:

| Order | CentralDef | SideDef | Midfield | SidePassing | SideWinger | CenterPassing |
|---|---:|---:|---:|---:|---:|---:|
| Defensive | 0.050 | 0.148 | 0.054 | 0.185 | 0.044 | 0.009 |
| TowardsMiddle | 0.047 | 0.093 | 0.082 | 0.160 | 0.043 | 0.026 |
| Offensive | 0.016 | 0.055 | 0.054 | 0.247 | 0.062 | 0.024 |
| Normal | 0.037 | 0.104 | 0.065 | 0.219 | 0.054 | 0.018 |

Central defence ve side defence `Defending`; midfield `Playmaking`; side attack `Passing + Winger`; central attack `Passing` katkısından oluşur.

### 3.6 Forward

Forward için üç ana davranış durumu bulunur.

`TowardsWing`:

```text
Midfield: Playmaking * 0.024
Ana side attack: Scoring * 0.093 + Passing * 0.101 + Winger * 0.044
Opposite side (side forward): Scoring * 0.018 + Passing * 0.034
Central attack: Passing * 0.102 + Scoring * 0.044
```

`Defensive`:

```text
Midfield: Playmaking * 0.058
Ana side attack: Scoring * 0.030 + Passing * 0.033 + Winger * 0.059
Opposite side (side forward): Scoring * 0.030 + Passing * 0.033
Central attack: Scoring * 0.102 + Passing * 0.108
```

`Normal/default`:

```text
Midfield: Playmaking * 0.041
Ana side attack: Scoring * 0.058 + Passing * 0.048 + Winger * 0.032
Opposite side (side forward): Scoring * 0.058 + Passing * 0.048
Central attack: Scoring * 0.178 + Passing * 0.066
```

Her contribution ilgili form multiplier ile çarpılır.

---

## 4. Rating context matematiği

`RegionalRatingEngine.ApplyContext(...)` rating hesaplandıktan sonra context etkilerini uygular.

### 4.1 Ev / derby midfield

İlk midfield çarpanı:

```text
Home      = 1.19892
DerbyAway = 1.11493
Other     = 1.00000
```

### 4.2 Team Attitude

```text
MatchOfTheSeason = 1.1149
PlayItCool       = 0.83945
Normal           = 1.0000
```

Bu çarpan midfield'e uygulanır.

### 4.3 Team Tactic

Counter Attack:

```text
Midfield *= 0.93
```

Ek savunma/atak etkileri:

```text
AttackMiddle:
    LeftDefence *= 0.85
    RightDefence *= 0.85

AttackWings:
    CentralDefence *= 0.85

Creative:
    LeftDefence *= 0.93
    CentralDefence *= 0.93
    RightDefence *= 0.93

LongShots:
    LeftAttack *= 0.96
    CentralAttack *= 0.96
    RightAttack *= 0.96
```

### 4.4 Stamina decay

`MatchMinute > 0` ise sadece midfield'e uygulanır:

```text
minute = clamp(MatchMinute, 0, 120)
Midfield *= 1 - 0.10 * clamp(minute / 90, 0, 1)
```

Dolayısıyla 90 dakika ve üzeri için bu katmandaki midfield decay üst sınırı `%10`'dur.

### 4.5 Lead retreat

Koşul:

```text
GoalDifference >= 2 && IgnoreLeadRetreat == false
```

Adım:

```text
steps = min(GoalDifference - 1, 7)
```

Defence protection:

```text
protection = 1 + steps * 0.075
```

Attack reduction:

```text
attack = 1 - steps * 0.09
```

Ardından üç defence sektörüne protection, üç attack sektörüne attack çarpanı uygulanır.

---

## 5. Questionnaire context matematiği

`QuestionnaireContext.cs` içindeki `QuestionnaireRatingAdjuster` de coach/spirit etkileri uygular.

Coach:

```text
Offensive -> attack 1.08, defence 0.89
Defensive -> attack 0.92, defence 1.14
Neutral   -> attack 1.00, defence 1.00
```

Team Spirit lookup:

```text
Murderous          0.72
Furious            0.86
Irritated          0.93
Composed           1.00
Calm               1.07
Content            1.14
Satisfied          1.21
Delirious          1.28
WalkingOnClouds    1.35
ParadiseOnEarth    1.42
```

Bu yardımcı adjuster midfield'e spirit, defence sektörlerine defence multiplier, attack sektörlerine attack multiplier uygular.

M7 scenario katmanında ise `RegionalRatingScenarioEngine` ayrı bir Team Spirit eğrisi kullanır:

```text
TeamSpiritMultiplier(x) = 0.10 + 0.425 * sqrt(clamp(x, 0, 10))
```

ve bunu yalnız midfield'e uygular. Coach çarpanları ayrıca attack/defence'ye uygulanır.

Bu iki kod yolunun aynı şey olmadığı açıkça korunmalıdır: `QuestionnaireRatingAdjuster` ve `RegionalRatingScenarioEngine` ayrı yardımcı/engine katmanlarıdır; birinin katsayısı diğerine otomatik olarak eşitlenmemelidir.

---

## 6. Display rating

`RegionalRatingEngine` raw sektör toplamlarını `Display(...)` ile display rating'e dönüştürür.

Bu belge, dosyanın geri kalanında `Display` fonksiyonunun tam lookup/formül metni alınmadan yeni bir formül uydurmamaktadır.

Dolayısıyla:

```text
Raw rating -> RegionalRatingEngine.Display(raw) -> Display rating
```

şeklinde belgelenir.

---

## 7. Possession modeli — M8

Dosya:

`HattrickAI_V5/Core/M8ChanceAllocationEngine.cs`

`CalculatePossessionProbability(ownMidfieldRating, opponentMidfieldRating)`:

Önce rating'ler dönüştürülür:

```text
own      = max(0, ownMidfieldRating) * 4 - 3
opponent = max(0, opponentMidfieldRating) * 4 - 3
```

Ardından negatifler sıfırlanır ve küp alınır:

```text
ownPower      = own^3
opponentPower = opponent^3
```

Sonuç:

```text
POS = ownPower / (ownPower + opponentPower)
```

Toplam 0 ise:

```text
POS = 0.5
```

Sonuç `[0,1]` aralığına clamp edilir.

Bu, kodda doğrudan uygulanan possession formülüdür.

---

## 8. Paper normal chance modeli

Kod sabitleri:

```text
ExclusiveChancesPerTeam = 5
OpenChancePool = 5

Left share            = 0.2565
Centre share          = 0.3615
Right share           = 0.2565
Direct FK share       = 0.0586
Indirect FK share     = 0.0418
Penalty share         = 0.0251
Expected normal       = 10.0
```

Regular sector share:

```text
0.2565 + 0.3615 + 0.2565 = 0.8745
```

Beklenen regular-sector chance sayısı:

```text
10.0 * 0.8745 = 8.745
```

M8, ayrıca takım başına 5 exclusive chance ve 5 open-chance pool kullanır.

Open pool possession'a göre dağıtılır:

```text
OwnOpenExpected      = 5 * POS
OpponentOpenExpected = 5 * (1 - POS)
```

Pressing dışı normal hacim faktörü:

```text
normalVolumeFactor = 1 - pressingSuppression
```

Regular expected chance:

```text
Own    = 8.745 * POS * normalVolumeFactor
Opp    = 8.745 * (1 - POS) * normalVolumeFactor
```

---

## 9. Taktik conversion matematiği

M8'de taktik dönüşüm iki giriş seviyesi kabul eder:

1. V5 iç taktik gücü: `0–10`
2. paper tactic rating: `RT`

`TacticPaperMappingEngine` açık bridge kullanır:

```text
RT = clamp(V5Level, 0, 10) * 2
```

Dolayısıyla:

```text
V5 0 -> RT 0
V5 5 -> RT 10
V5 10 -> RT 20
```

Bu dönüşüm M8'in paper Equation B.2 implementation'ına girer.

### Counter Attack

```text
-0.617941717072569
+ 0.104274398 * RT
- 0.00358354796 * RT^2
+ 0.0000434356 * RT^3
```

### Attack Middle

```text
-0.00036765 * RT^2
+ 0.02180462 * RT
+ 0.0705084
```

### Attack Wings

```text
-0.00046569 * RT^2
+ 0.02894608 * RT
+ 0.10514706
```

### Long Shots

```text
0.00761935 * RT + 0.07520052
```

### Pressing

```text
-0.00780421 * RT^2
+ 0.471402 * RT
- 1.10735
```

Sonuç her durumda `[0,1]` aralığında clamp edilir.

---

## 10. Taktiklerin chance dağılımına etkisi

### Attack Middle

Moved amount:

```text
movedMiddle = (left + right) * TCR
```

Bu miktar left/right'dan oransal alınarak centre'a taşınır.

### Attack Wings

Moved amount:

```text
movedWings = centre * TCR
```

Centre'dan çıkarılır ve eşit olarak left/right'a eklenir.

### Set-piece share

Set-piece başlangıç toplamı:

```text
0.0586 + 0.0418 + 0.0251 = 0.1255
```

Final tuple oluşturulurken left/centre/right/set-piece toplamı normalize edilir.

---

## 11. Counter Attack ve Pressing özel etkileri

### Counter Attack

M8 `Calculate(...)` içinde effective midfield:

```text
effectiveOwnMidfield = ownMidfield * (1 - 0.07)
```

sadece Counter Attack için uygulanır.

Sonuçta possession bu azaltılmış midfield üzerinden hesaplanır.

Ayrıca:

```text
CounterAttackEligible = tactic == CounterAttack
                         && ownMidfieldRating < opponentMidfieldRating
```

şeklinde boolean üretilir.

### Pressing

Pressing seçilmişse suppression değeri conversion rate'ten gelir:

```text
pressingSuppression = TCR
normalVolumeFactor = 1 - TCR
```

Bu değer hem own hem opponent regular/open chance hacmini düşürür.

---

## 12. M7 -> M8 matematik akışı

Gerçek kod ilişkisi:

```text
RegionalPlayer[]
      |
      v
RegionalRatingEngine
      |
      +--> 7 raw regional ratings
      |
      v
RegionalRatingScenarioEngine
      |
      +--> questionnaire/context adjustments
      |
      v
M7 RatingScenarioResult
      |
      +--> midfield rating
      +--> TeamTactic
      |
      v
AdvancedTacticalScenarioEngine
      |
      +--> tactic strength (0-10)
      |
      v
M8ChanceAllocationEngine
      |
      +--> possession
      +--> tactic conversion rate
      +--> sector shares
      +--> expected regular chances
```

Kritik nokta: M8 aldığı taktiğin etkisini hesaplar; burada taktik seçimi yapılmaz.

---

## 13. Production'daki taktik gerçeği

`AnalysisService` production analysis path'te `RatingContext` oluştururken `TeamTactic.Normal` veriyor.

Bu nedenle M7/M8 matematiği production web path'te şu anda input olarak `Normal` alır.

`AdvancedTacticalScenarioEngine` ve `M8ChanceAllocationEngine` AttackMiddle/AttackWings/CounterAttack/Pressing gibi seçeneklerin sonuçlarını hesaplayabilir; fakat bu dosyalar kendi başlarına production final tactic selector değildir.

Bu nedenle dokümantasyonda:

```text
Matematik: mevcut
Taktik consequence calculation: mevcut
Taktik selection: production web path'te mevcut değil
```

ayrımı korunur.

---

## 14. Doğrulanmış formül özeti

```text
EffectiveSkill
  = BaseSkill + LoyaltyEffect + (ExperienceBonus - 1.13)

FormMultiplier
  = FormFactor(Form) / 0.755

Possession
  = A^3 / (A^3 + B^3)

A = max(0, max(0, OwnMidfield) * 4 - 3)
B = max(0, max(0, OppMidfield) * 4 - 3)

Regular expected chances
  = 8.745 * POS * (1 - PressingSuppression)

Paper RT
  = clamp(V5 tactical level, 0, 10) * 2

TCR
  = tactic-specific polynomial(RT)
```

Bu özet yalnız repository'de doğrudan bulunan denklemleri içerir.

---

## 15. Aşama 3 sonucu

Aşama 3 kapsamında V5'in gerçek kodundan doğrulanan matematik katmanları dokümante edilmiştir.

Bir sonraki aşama: **Aşama 4 — Motor teknik dokümanları**.
