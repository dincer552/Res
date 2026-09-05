# HattrickAI V5 — Aşama 4: Motor Teknik Dokümanları

## 1. Amaç

Bu belge Aşama 4 kapsamında V5 production kodunda bulunan motorların görevlerini, gerçek input/output sözleşmelerini, ana fonksiyonlarını ve motorlar arası bağlantılarını belgeler.

Kural: Kodda olmayan bir davranış bu belgeye eklenmez. Bir motorun selector olmadığı yerde selector varmış gibi anlatılmaz.

Production ana akışı:

```text
M3 → M4 → M5 → M6-A
                  ↓
            M7 → M7.2 → M8 → M9
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
                  ↓
          FinalMatchPlan / Prediction
```

---

# 2. M3 — Player Analysis Engine

**Dosya:** `HattrickAI_V5/Core/PlayerAnalysisEngine.cs`

**Class:** `PlayerAnalysisEngine`

**Interface:** `IPlayerAnalysisEngine`

### Görev

Oyuncu listesinden her oyuncunun uygunluk ve pozisyon skor profilini üretir. XI seçmez, diziliş seçmez, rakip değerlendirmez ve takım ratingi üretmez.

### Ana fonksiyonlar

`Analyze(IReadOnlyList<Player>)`

Tüm oyuncuları `AnalyzePlayer` üzerinden analiz eder ve `PlayerAnalysisResult` döndürür.

`AnalyzePlayer(Player)`

Oyuncunun eligibility durumunu, pozisyon skorlarını ve specialty context'ini üretir.

`Score(Player, positionCode)`

Belirli pozisyon için gerçek V5 skorunu hesaplar.

### Eligibility

```text
Id > 0 && InjuryLevel != 999
```

### Pozisyon skorları

```text
GK       = Keeper + Form * 0.15
DEF-L/R  = Defending + Passing*0.10 + Winger*0.05
DEF-C/CL/CR = Defending*1.05 + Passing*0.15 + Playmaking*0.04
W-L/R    = Winger + Passing*0.22 + Playmaking*0.08
IM-L/R   = Playmaking + Passing*0.25 + Stamina*0.12
IM-C     = Playmaking*1.05 + Passing*0.25 + Stamina*0.12 + Experience*0.04
FW-L/R   = Scoring + Passing*0.18 + Winger*0.08 + Experience*0.02
FW-C     = Scoring*1.05 + Passing*0.20 + Playmaking*0.04
```

### Output

`PlayerAnalysisResult → PlayerAnalysisProfile[] → PlayerPositionCandidate[]`

Primary ve secondary position, skor sıralamasından belirlenir.

Specialty M3'te rating bonusu değildir; sonraki event/tactical katmanlar için profile taşınır.

---

# 3. M4 — Formation Candidate Engine

**Dosya:** `HattrickAI_V5/Core/FormationCandidateEngine.cs`

**Class:** `FormationCandidateEngine`

### Görev

M3 oyuncu profillerinden doldurulabilir yasal formasyonları üretir. Oyuncu-slot final eşleştirmesi M5'e bırakılır.

### Yasal registry

```text
3-5-2
3-4-3
4-4-2
4-5-1
2-5-3
5-3-2
```

Her formasyon 11 slot koduyla tanımlıdır.

### Ana fonksiyonlar

`Generate(MatchDataContext, PlayerAnalysisResult)`

Context'i doğrulayıp oyuncu tabanlı generation'a aktarır.

`Generate(PlayerAnalysisResult)`

Eligible oyuncuları ID bazında deduplicate eder, her legal formation için feasibility kontrolü yapar ve structural score üretir.

### Feasibility

Recursive `TryAssign` ile her slotun pozitif skorlu uygun oyuncuya atanıp atanamayacağı kontrol edilir.

### Structural score

Her slot için mevcut uygun oyuncular arasından en yüksek skor seçilerek toplam skor / slot sayısı hesaplanır.

Bu skor final maç sonucu değildir; formasyonun yapısal doldurulabilirliğini temsil eder.

### Output

`FormationCandidateSet → FormationCandidate[]`

---

# 4. M5 — Position Optimization Engine

**Dosya:** `HattrickAI_V5/Core/PositionOptimizationEngine.cs`

**Class:** `PositionOptimizationEngine`

### Görev

M4 formasyonunda oyuncu-slot eşleştirmelerini üretir ve alternatif XI havuzu oluşturur.

Rakip, takım taktiği ve maç tahmini M5'in görevi değildir.

### Ana fonksiyonlar

`GenerateCandidates(context, players, formation, maxCandidates)`

Bir formasyon için XI adaylarını üretir.

`GenerateCandidates(context, players, FormationCandidateSet, maxCandidatesPerFormation)`

Tüm formasyon adaylarını toplu işler.

### Kontroller

- minimum 11 eligible oyuncu,
- her slot için en az bir feasible oyuncu,
- pozitif finite structural score,
- 11 benzersiz oyuncu,
- 11 benzersiz slot.

### Exact optimization

İlk en iyi atama rectangular Hungarian algorithm ile bulunur.

M5 cost matrix'te pozitif adjusted score negatif cost olarak kullanılır; amaç toplam adjusted score'u maksimize eden eşleşmeyi bulmaktır.

### Alternative search

Beam search:

```text
BeamWidth = 2500
DefaultMaxCandidates = 100
```

Slotlar feasibility count'a göre sıralanır. Her ara durumda benzersiz oyuncu kullanılır. Beam her aşamada skor ve deterministic assignment key ile sıralanır.

### Role adjustment

```text
NaturalRoleTieThreshold = 0.75
PrimaryRoleBonus = 0.05
SecondaryRoleBonus = 0.02
RoleTieEpsilon = 0.05
```

Bu bonuslar yalnızca oyuncunun doğal/ikincil rolü score farkı eşiği içinde olduğunda ve tied-best durumu oluşmadığında uygulanır.

### Output

`PositionAssignmentCandidate[]`

Her candidate:

- formation,
- lineup,
- suitability score,
- player assignments,
- structural score

taşır.

---

# 5. M6 — Behaviour Candidate + Global Optimization

M6 iki gerçek kod katmanından oluşur:

- `BehaviourEngine.cs`
- `BehaviourCandidateEngine.cs`
- `M6GlobalOptimizationEngine.cs`

## 5.1 BehaviourEngine

**Dosya:** `HattrickAI_V5/Core/BehaviourEngine.cs`

Position bazında legal `PlayerOrder` seçeneklerini tanımlar.

```text
GK: Normal

Wing Back:
Normal, Offensive, Defensive, TowardsMiddle

Central Defender:
Normal, Offensive, TowardsWing

Winger:
Normal, Offensive, Defensive, TowardsMiddle

Inner Midfielder:
Normal, Offensive, Defensive, TowardsWing

Forward:
Normal, Defensive, TowardsWing
```

Bu sınıf scorer veya winner selector değildir.

## 5.2 BehaviourCandidateEngine

**Dosya:** `HattrickAI_V5/Core/BehaviourCandidateEngine.cs`

`Build(Lineup, players)` her slot için legal order matrix üretir. Normal order her zaman baseline olarak korunur.

`EnumerateCompleteSets` Cartesian product'ı yalnızca kombinasyon sayısı `maxSets` sınırı içindeyse tamamen enumerate eder. Varsayılan üst sınır `100000`.

## 5.3 M6GlobalOptimizationEngine

**Dosya:** `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs`

### Görev

Sabit bir XI için bireysel davranış kombinasyonlarını formation-aware bounded search ile değerlendirir.

### Input

- XI candidates,
- player listesi,
- `Func<Lineup, ..., TacticalCandidate>` evaluator,
- beam width,
- iteration budget,
- opsiyonel formation budget'ları.

### Search

Aynı formasyon ayrı bir search grubu olarak işlenir.

Normal M6-A baseline'da tüm geçerli slot order'ları `Normal` yapılır.

M6-B'de `preserveInputOrders=true` olduğundan gelen order'lar korunur.

Her iterasyonda mevcut beam'deki her oyuncu için legal farklı order'lar frontier oluşturur. Frontier evaluator üzerinden puanlanır ve beam genişliğine göre tutulur.

### Determinism

Eşit skor durumunda lineup signature deterministic tie-break olarak kullanılır.

### Budget

Production pipeline:

```text
M6-A base beam = 6
M6-A max iterations = 4
M6-B base beam = 6
M6-B base iterations = 3
```

M6-B formation budget'ları M10 formation rank'ına göre tier'lara ayrılır.

### Output

`M6OptimizationResult`:

- BestCandidate
- TopCandidates
- Iterations
- EvaluatedCandidates
- RetainedCandidates
- Converged

---

# 6. M7 — Regional Rating Scenario Engine

**Dosya:** `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs`

**Class:** `RegionalRatingScenarioEngine`

### Görev

Tek bir aday senaryonun bölgesel rating sonucunu hesaplar. Scenario seçmez.

### Input

`RegionalPlayer[] + MatchState`

### İşlem

Önce `RegionalRatingEngine` base rating'i hesaplar.

Sonra questionnaire context uygulanır:

- Team Spirit → midfield,
- Coach style → attack/defence.

`RatingScenarioResult` döner.

### Team Spirit

```text
0.10 + 0.425 * sqrt(clamp(teamSpirit, 0, 10))
```

Sadece midfield'e uygulanır.

### Coach

```text
Offensive: attack 1.08 / defence 0.89
Defensive: attack 0.92 / defence 1.14
Neutral:   1.00 / 1.00
```

### MatchState

Taşınan alanlar:

- CandidateId
- FormationId
- LineupId
- BehaviourSetId
- MatchLocation
- TeamAttitude
- TeamTactic
- TeamSpirit
- CoachStyle
- MatchMinute
- GoalDifference
- IgnoreLeadRetreat
- Confidence

### Output

`RatingScenarioResult` ve `RatingModifiers`.

---

# 7. M7.2 — Advanced Tactical Scenario Engine

**Dosya:** `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs`

**Class:** `AdvancedTacticalScenarioEngine`

### Kritik görev sınırı

M7.2 verilen `TeamTactic` değerini scenario'ya çevirir ve o taktiğin gücünü/sonuçlarını hesaplar. Production web path'te kendi başına final takım taktiği seçmez.

### Desteklenen tactic enum

```text
Normal
Pressing
CounterAttack
AttackMiddle
AttackWings
LongShots
Creative
```

### Tactic skill

Ortalama outfield skill'leri üzerinden:

```text
AttackMiddle / AttackWings = average Passing
CounterAttack = (Defending + 2*Passing) / 3
Creative = (Passing + Experience) / 2
LongShots = (3*Scoring + Passing) / 4
Pressing = (Defending + Stamina) / 2
```

Sonra:

```text
tacticSkill = clamp(skill / 2, 0, 10)
```

### TacticalLevel

0–10 aralığına normalize edilir. Opponent average main skill verilirse code içinde 6.0 baseline etrafında küçük bir modifier uygulanır.

### Output

`AdvancedTacticalScenarioResult`:

- tactic,
- tactical skill aggregate,
- tactical level,
- input totals,
- chance distribution,
- tactic-specific profiles,
- M8 context.

---

# 8. M8 — Chance Model

**Dosyalar:**

- `HattrickAI_V5/Core/M8ChanceModel.cs`
- `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs`

### Görev

M7 rating + M7.2 tactical scenario + opponent rating üzerinden chance ownership, sector matchup ve tactic-specific chance volumes hesaplar.

### Possession

M8 allocation engine:

```text
own = max(0, ownMidfield) * 4 - 3
opp = max(0, opponentMidfield) * 4 - 3

ownPower = own^3
oppPower = opp^3

POS = ownPower / (ownPower + oppPower)
```

Toplam güç 0 ise `0.5`.

### Paper base shares

```text
Left     0.2565
Centre   0.3615
Right    0.2565
Direct FK 0.0586
Indirect FK 0.0418
Penalty 0.0251
Expected normal attacks 10.0
```

Regular sector total:

```text
8.745
```

### Sector matchup

Own:

```text
LeftAttack vs opponent RightDefence
CentreAttack vs opponent CentralDefence
RightAttack vs opponent LeftDefence
```

Opponent için simetrik hesap yapılır.

`ScoreProbability` code'daki Eq.4 uygulamasıdır:

```text
a = max(0, attack) * 4 - 3
d = max(0, defence) * 4 - 3
attackPower = 0.92 * a^3.5
defencePower = d^3.5
P = attackPower / (attackPower + defencePower)
```

Her iki güç 0 ise `0.5`.

### Taktik etkileri

Counter Attack:

```text
own midfield * (1 - 0.07)
```

Pressing:

```text
normalVolumeFactor = 1 - pressingSuppression
```

Counter Attack chance:

```text
missedOpponentNormal = opponentRegularChance * (1 - opponentRegularQuality)
counterAttackExpected = missedOpponentNormal * counterAttackConversionRate
```

Long Shots:

```text
longShotExpected = ownRegularChance * LongShotConversionRate
normalAfterLS = ownRegularChance - longShotExpected
```

Attack Middle ve Attack Wings sector paylaştırması M7.2 `ChanceDistributionEffect` ile M8'e taşınır.

### Output

`M8ChanceResult`:

- midfield share,
- üç sector matchup probability,
- sector shares,
- set-piece share,
- structural chance index,
- chance allocation,
- tactic-specific volumes.

---

# 9. M9 — Match Prediction Engine

**Dosya:** `HattrickAI_V5/Core/M9MatchPredictionEngine.cs`

**Class:** `M9MatchPredictionEngine`

### Görev

M8 chance sonuçlarını goal expectation ve W/D/L olasılıklarına çevirir. Oyuncu/event verisi varsa M9 event engine ile specialty/event etkileri de dahil edilir.

### Normal chance goals

M8'den gelen regular chance hacmi ve quality kullanılır. Opponent event pressing signal normal volume'u azaltabilir.

Set-piece expected:

```text
10.0 * 0.1255 * chanceShare
```

Neutral conversion:

```text
0.5
```

### Event layer

Own lineup + players mevcutsa `M9EventGoalEngine` çağrılır.

Opponent lineup + players mevcutsa rakip perspektifi de hesaplanır. Böylece event effects yalnızca kendi takımına uygulanmış olmaz.

### Expected goals

Kodda normal goals, counter-attack, set-piece ve event/special contributions birleştirilir ve `0.05–5.0` aralığına clamp edilir.

### W/D/L

Independent Poisson distributions kullanılır.

```text
P(k) = exp(-lambda) * lambda^k / k!
```

Production implementation goal cutoff olarak `20` kullanır ve tüm own/opponent goal kombinasyonlarını toplar.

### Output

`M9PredictionResult` ve içindeki `MatchPrediction`.

Ayrıca:

- MostLikelyScore
- PredictedResult
- ConfidenceLabel
- EventGoals
- OpponentEventGoals
- Simulation

alanları bulunur.

---

# 10. DB1 — Candidate Database #1

**Dosya:** `HattrickAI_V5/Core/CandidateEvaluationDatabase.cs`

DB1 kalıcı database veya ML modeli değildir. Tek analiz oturumundaki candidate search pool'dur.

### Kapasite

```text
DefaultCapacity = 100
MaxPerFormation = 30
MinimumPerFormation = 12
```

### Kayıt

`CandidateEvaluationRecord` şunları taşır:

- CandidateId
- Formation
- Lineup
- M5 suitability/structural score
- TacticalScore
- Rating
- Advanced
- Chance
- Prediction
- RankingScore
- Stage

M6-A production ranking:

```text
RankingScore = 0.70 * TacticalScore + 0.30 * WinProbability
```

### Diversity

`TopWithFormationDiversity` önce required formations için minimum depth rezerve eder, sonra kalan kapasiteyi global ranking ile doldurur.

Altı legal formasyon ve minimum 12 derinlik için korunan teorik minimum:

```text
6 * 12 = 72
```

DB1 production'da M10'a taşınan havuzun her legal formasyonu içermesi ayrıca kontrol edilir.

---

# 11. M10 — Final Decision / Formation Competition

**Dosya:** `HattrickAI_V5/Core/M10FinalDecisionEngine.cs`

### Görev

DB1'den gelen adayları formation-aware biçimde karşılaştırır ve final plan için deterministik winner seçer.

### Composite score

Varsayılan ağırlıklar:

```text
Tactical   0.55
Prediction 0.30
Structural 0.15
```

Tactical score önce logistic normalization'dan geçirilir:

```text
1 / (1 + exp(-clamp(tactical, -20, 20)))
```

Prediction olarak Monte Carlo simulation win probability kullanılır.

Structural score `[0,1]` aralığına clamp edilir.

### Formation competition

Adaylar formation'a göre gruplanır. Her formasyonun en iyi composite adayının rank'ı, margin'i, candidate count'u ve simulation W/D/L bilgileri yayınlanır.

### Auto approach

`SelectApproach` üç legal `TeamAttitude` seçeneğini karşılaştırabilir:

```text
Normal
PlayItCool
MatchOfTheSeason
```

Bu **TeamTactic selector değildir**.

---

# 12. M6-B — Rank-driven refinement

M6-B aynı `M6GlobalOptimizationEngine` üzerinden çalışır fakat M10 formation rank'ına göre budget alır ve `preserveInputOrders=true` kullanır.

Production base budget:

```text
BeamWidth = 6
MaxIterations = 3
```

Rank tier mantığı:

```text
Tier 0: max(baseBeamWidth, 8), max(baseIterations, 4)
Tier 1: max(baseBeamWidth, 6), max(baseIterations, 3)
Tier 2: max(4, baseBeamWidth-1), max(2, baseIterations-1)
```

Böylece M10'da üst sıradaki formasyon daha derin refinement bütçesi alır.

Çıktı ikinci candidate pool'a yazılır.

---

# 13. DB2 — Candidate Database #2

DB2 aynı `CandidateEvaluationDatabase` sınıfının `SecondPass` instance'ıdır.

Production görevi M6-B refinement sonrası finalist adayları taşımaktır.

DB2 de:

```text
Capacity = 100
MaxPerFormation = 30
MinimumPerFormation = 12
```

kurallarına tabidir.

DB2'nin M11'e taşınan exposed finalist havuzunda legal formasyonların tamamının bulunması production pipeline tarafından kontrol edilir.

---

# 14. M11 — Final Selector

**Dosya:** `HattrickAI_V5/Core/M11FinalSelectorEngine.cs`

### Görev

DB2 finalistlerini son kez karşılaştırır ve `FinalMatchPlan` ile prediction'ı üretir.

### Final score

```text
Tactical normalized       * 0.35
Monte Carlo win            * 0.35
Structural                 * 0.15
Stability                  * 0.05
Risk-adjusted outcome      * 0.10
```

Risk-adjusted outcome:

```text
win + 0.50 * draw
```

Tactical normalization:

```text
1 / (1 + exp(-clamp(tactical, -20, 20)))
```

Structural ve stability `[0,1]` aralığına clamp edilir.

### Deterministic tie-break

Sırasıyla:

1. FinalScore
2. Monte Carlo win probability
3. TacticalScore
4. formation adı
5. lineup signature

ile sıralama deterministik tutulur.

Varsayılan ranking output'u ilk `topRankingCount=20` adayı döndürür; winner ise sıralanmış listenin ilkidir.

### Output

`M11DecisionResult`:

- `BestPlan`
- `Prediction`
- `Ranking`
- `CandidateCount`
- `FormationCount`

---

# 15. Motorlar arası gerçek production bağlantısı

`MotorPipelineService.RunAsync` içindeki gerçek zincir:

```text
M3
PlayerAnalysisResult
   ↓
M4
FormationCandidateSet
   ↓
M5
PositionAssignmentCandidate[]
   ↓
M6-A
behaviour search
   ↓
M7
RegionalRatingScenarioResult
   ↓
M7.2
AdvancedTacticalScenarioResult
   ↓
M8
M8ChanceResult
   ↓
M9
M9PredictionResult
   ↓
Candidate DB #1
   ↓
M10
formation competition / final review
   ↓
M6-B
rank-driven refinement
   ↓
Candidate DB #2
   ↓
M11
FinalMatchPlan + final prediction
```

M6 evaluator içinde M7, M7.2, M8 ve M9 downstream evaluation olarak birlikte çalışır. Bu nedenle M6 yalnızca "davranış üretir"; winner scoring downstream evaluator'dan gelir.

---

# 16. Production'daki önemli sınırlar

## Taktik selector

Mevcut web production path'te `AnalysisService` `RatingContext` oluştururken `TeamTactic.Normal` verir. M7.2 ve M8 verilen taktiğin sonuçlarını hesaplar.

Dolayısıyla mevcut sistem için:

```text
TeamTactic = Normal input
```

vardır; `AttackMiddle` veya `AttackWings` final selector tarafından seçiliyor denemez.

## Calibration durumu

M7.2 ve M8 research-backed yapı kullanırken bazı sonuçların historical match calibration gerektirdiği kodda açıkça işaretlenmiştir. M9 prediction result da `StructuralModelAwaitingHistoricalCalibration` status'u taşıyabilir.

Bu belge calibration tamamlanmış gibi bir sonuç iddia etmez.

---

# 17. Kaynak dosya özeti

| Katman | Dosya | Ana class |
|---|---|---|
| M3 | `Core/PlayerAnalysisEngine.cs` | `PlayerAnalysisEngine` |
| M4 | `Core/FormationCandidateEngine.cs` | `FormationCandidateEngine` |
| M5 | `Core/PositionOptimizationEngine.cs` | `PositionOptimizationEngine` |
| M6 | `Core/BehaviourEngine.cs` | `BehaviourEngine` |
| M6 | `Core/BehaviourCandidateEngine.cs` | `BehaviourCandidateEngine` |
| M6 | `Core/M6GlobalOptimizationEngine.cs` | `M6GlobalOptimizationEngine` |
| M7 | `Core/RegionalRatingScenarioEngine.cs` | `RegionalRatingScenarioEngine` |
| M7.2 | `Core/AdvancedTacticalScenarioEngine.cs` | `AdvancedTacticalScenarioEngine` |
| M8 | `Core/M8ChanceModel.cs` | `M8ChanceModel` |
| M8 | `Core/M8ChanceAllocationEngine.cs` | `M8ChanceAllocationEngine` |
| M9 | `Core/M9MatchPredictionEngine.cs` | `M9MatchPredictionEngine` |
| DB1/DB2 | `Core/CandidateEvaluationDatabase.cs` | `CandidateEvaluationDatabase` |
| M10 | `Core/M10FinalDecisionEngine.cs` | `M10FinalDecisionEngine` |
| M11 | `Core/M11FinalSelectorEngine.cs` | `M11FinalSelectorEngine` |
| Pipeline | `Core/MotorPipelineService.cs` | `MotorPipelineService` |

---

# 18. Aşama 4 sonucu

Aşama 4 kapsamında M3, M4, M5, M6-A, M7, M7.2, M8, M9, DB1, M10, M6-B, DB2 ve M11 gerçek production kodları üzerinden belgelenmiştir.

Sonraki aşama: **Aşama 5 — Gerçek maç örnek analizi.**
