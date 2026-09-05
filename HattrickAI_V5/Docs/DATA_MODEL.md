# Hattrick AI V5 — Veri Modeli

## 1. Amaç

Bu belge V5 repository kodunda doğrulanabilen temel veri modellerini ve modeller arasındaki veri akışını tanımlar.

Kapsam:

- oyuncu modeli,
- oyuncu analiz profili,
- lineup / slot modeli,
- maç ve analiz context'i,
- rakip maç profili,
- rating ve scenario modelleri,
- aday XI ve tactical candidate modelleri,
- DB1 / DB2 kayıt modeli,
- final plan ve prediction modelleri.

Kodda karşılığı bulunmayan alanlar bu belgede özellik olarak yazılmaz.

---

## 2. Kanonik veri sözleşmesi

Motorlar birbirlerinin iç implementasyonuna doğrudan bağlanmak yerine ortak contract modelleri üzerinden haberleşir.

Ana contract dosyası:

`HattrickAI_V5/Core/MotorPipelineContracts.cs`

Temel zincir:

```text
CHPP / questionnaire
        |
        v
MatchDataContext
        |
        +--> PlayerAnalysisResult
        |
        +--> FormationCandidateSet
        |
        +--> PositionAssignmentCandidate[]
        |
        +--> TacticalCandidate / rating / matchup / chance
        |
        +--> MatchPrediction
        |
        +--> FinalMatchPlan
```

---

## 3. Player modeli

Dosya:

`HattrickAI_V5/Core/Models.cs`

Class/record:

`Player`

Alanlar:

| Alan | Tip | Kod anlamı |
|---|---|---|
| `Id` | int | Oyuncu ID |
| `Name` | string | Oyuncu adı |
| `Keeper` | int | Keeper skill |
| `Defending` | int | Defending skill |
| `Playmaking` | int | Playmaking skill |
| `Passing` | int | Passing skill |
| `Winger` | int | Winger skill |
| `Scoring` | int | Scoring skill |
| `Stamina` | int | Stamina skill |
| `Form` | int | Form |
| `Experience` | int | Experience |
| `Loyalty` | int | Loyalty |
| `InjuryLevel` | int | Injury seviyesi; analizde ayrıca eligibility kontrolünde kullanılır |
| `Specialty` | `PlayerSpecialty` | Specialty enum değeri |
| `SetPiecesSkill` | int | Set-piece skill |

`PlayerSpecialty` enum değerleri:

```text
None = 0
Technical = 1
Quick = 2
Powerful = 3
Unpredictable = 4
Head = 5
```

Player verisi production web path'te `AnalysisService.ReadPlayers(...)` üzerinden CHPP `players` endpointinden okunur.

---

## 4. Oyuncu analiz modeli

Dosya:

`HattrickAI_V5/Core/PlayerAnalysisEngine.cs`

### PlayerAnalysisResult

```text
PlayerAnalysisResult
    -> IReadOnlyList<PlayerAnalysisProfile>
```

### PlayerAnalysisProfile

Bir oyuncunun analiz edilmiş pozisyon profilini taşır:

- `PlayerId`
- `PlayerName`
- `IsEligible`
- `InjuryLevel`
- `Specialty`
- `SpecialtyProfile`
- `Positions`
- `PrimaryPosition`
- `SecondaryPosition`

Ek hesaplanan özellikler:

- `PrimaryScore`
- `SecondaryScore`

### PlayerPositionCandidate

Her pozisyon için:

- `PositionCode`
- `Score`

tutar.

M3'ün desteklediği position code seti kodda şu şekildedir:

```text
GK
DEF-L / DEF-CL / DEF-C / DEF-CR / DEF-R
W-L / IM-L / IM-C / IM-R / W-R
FW-L / FW-C / FW-R
```

Eligibility:

`player.Id > 0 && player.InjuryLevel != 999`

olarak uygulanır.

Specialty profile M3 aşamasında doğrudan rating bonusu haline getirilmez; sonraki aşamalar için context olarak taşınır.

---

## 5. Slot modeli

Dosya:

`HattrickAI_V5/Core/Models.cs`

Record:

`Slot`

Alanlar:

- `Code`
- `Label`
- `Description`
- `PlayerName`
- `PlayerId`
- `Rating`
- `X`
- `Y`
- `Order`
- `HistoricalStars`

`Order` tipi:

`PlayerOrder`

Slot aynı zamanda frontend'in saha yerleşiminde kullanılabilecek presentation bilgisini taşır (`X`, `Y`, label/description).

---

## 6. Lineup modeli

Record:

`Lineup(string TeamName, string Formation, IReadOnlyList<Slot> Slots)`

Temel alanlar:

- takım adı,
- formation string,
- slot listesi.

JSON tarafında `Slots` iç property'si ignore edilerek `DisplaySlots` üzerinden normalize edilmiş slot listesi yayınlanır.

`DisplaySlots` duplicate position code oluşması halinde tanımlı alternatif slot code'ları kullanarak display setini normalize eder.

Bu normalization'ın amacı display contract'ını korumaktır; burada yeni bir oyuncu seçimi yapılmaz.

Lineup ayrıca:

- `OwnLineup`
- `OpponentLineup`

gibi `Analysis` property'lerinde doğrudan kullanılır.

---

## 7. MatchDataContext

Dosya:

`HattrickAI_V5/Core/MotorPipelineContracts.cs`

Record:

`MatchDataContext`

Alanlar:

| Alan | Tip |
|---|---|
| `OwnPlayers` | `IReadOnlyList<Player>` |
| `OwnTeamId` | int |
| `OwnTeamName` | string |
| `Opponent` | `OpponentMatchProfile` |
| `RatingContext` | `RatingContext` |
| `Questionnaire` | `MatchQuestionnaire` |
| `OpponentLineup` | `Lineup?` |
| `OpponentPlayers` | `IReadOnlyList<Player>?` |

Bu model M3-M11 pipeline'ının üst seviye input context'idir.

---

## 8. Questionnaire modeli

Dosya:

`HattrickAI_V5/Core/QuestionnaireContext.cs`

Record:

`MatchQuestionnaire`

Alanlar:

- `Coach`
- `TeamSpirit`
- `MatchImportance`

Enum'lar:

### CoachStyle

```text
Neutral
Offensive
Defensive
```

### TeamSpiritLevel

```text
Murderous
Furious
Irritated
Composed
Calm
Content
Satisfied
Delirious
WalkingOnClouds
ParadiseOnEarth
```

### MatchImportance

Kodda `TeamAttitude` tipi kullanılır.

Default questionnaire:

`CoachStyle.Neutral`

`TeamSpiritLevel.Composed`

`TeamAttitude.Normal`

---

## 9. OpponentMatchProfile

Dosya:

`HattrickAI_V5/Core/OpponentMatchProfile.cs`

Record:

`OpponentMatchProfile`

Temel alanlar:

- `TeamName`
- `Formation`
- `Rating`
- `Threat`
- `Players`
- `LastMatchLineup`

Türetilen rating alanları:

- `LeftAttack`
- `CentralAttack`
- `RightAttack`
- `LeftDefence`
- `CentralDefence`
- `RightDefence`
- `Midfield`

Türetilen threat alanları:

- `LeftAttackThreat`
- `CenterAttackThreat`
- `RightAttackThreat`

Production `AnalysisService` rakip profilini oluştururken rakibin son resmi/rekabetçi maçındaki lineup ve `matchdetails` rating verilerini kullanır.

---

## 10. MatchState modeli

Dosya:

`HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs`

`MatchState`, tek bir aday senaryosunun M7 tarafındaki çalışma durumudur.

Alanlar:

- `CandidateId`
- `FormationId`
- `LineupId`
- `BehaviourSetId`
- `MatchLocation`
- `TeamAttitude`
- `TeamTactic`
- `TeamSpirit`
- `CoachStyle`

Ek state alanları:

- `MatchMinute`
- `GoalDifference`
- `IgnoreLeadRetreat`
- `Confidence`

Kritik ayrım:

`TeamTactic` ve `TeamAttitude` farklı veri alanlarıdır.

---

## 11. Rating modelleri

`RegionalRatingScenarioEngine` tarafından üretilen ana sonuç:

`RatingScenarioResult`

Alanlar:

- `Rating`
- `State`
- `Confidence`
- `Modifiers`

`RatingModifiers` içinde:

- Team Spirit multiplier
- Match Location
- Team Attitude
- Team Tactic
- Coach Style
- Coach attack multiplier
- Coach defence multiplier

bulunur.

`RegionalRatingSnapshot` temel yedi bölgesel rating alanını taşır:

```text
Left Defence
Central Defence
Right Defence
Midfield
Left Attack
Central Attack
Right Attack
```

Aynı modelde raw ve display rating değerleri bulunur.

---

## 12. FormationCandidate modeli

Dosya:

`HattrickAI_V5/Core/MotorPipelineContracts.cs`

Alanlar:

- `Formation`
- `SlotCodes`
- `StructuralScore`

`FormationCandidateSet` ise:

`IReadOnlyList<FormationCandidate>`

tutar.

M4'ün yasal formasyon registry'si altı aday içerir:

```text
3-5-2
3-4-3
4-4-2
4-5-1
2-5-3
5-3-2
```

---

## 13. PositionAssignmentCandidate

M5 çıktısıdır.

Alanlar:

- `Formation`
- `Lineup`
- `SuitabilityScore`
- `PlayerAssignments`
- `StructuralScore`

`CandidateId`, lineup slotlarındaki:

`PositionCode:PlayerId`

çiftlerinin deterministik birleşiminden üretilir.

Bu model M5'in oyuncu-slot eşleştirme sonucudur.

---

## 14. BehaviourPlanCandidate

Contract modeli:

`BehaviourPlanCandidate`

Alanlar:

- `Lineup`
- `StructuralScore`
- `BehaviourScore`

M6 davranış aramasındaki lineup temsilinde kullanılır.

Ayrıca `PlayerOrder` alanı slot düzeyinde davranışın gerçek representation'ıdır.

---

## 15. TacticalCandidate

Alanlar:

- `Lineup`
- `Rating`
- `Matchup`
- `TacticalScore`

Bu model tek aday lineup için downstream tactical evaluation sonucunu temsil eder.

M6 evaluator zincirinde M7/M7.2/M8 sonuçlarından türetilir.

---

## 16. MatchupEvaluation

Alanlar:

- `MidfieldMargin`
- `LeftAttackMargin`
- `CentralAttackMargin`
- `RightAttackMargin`
- `LeftDefenceMargin`
- `CentralDefenceMargin`
- `RightDefenceMargin`
- `OverallScore`

Bu model kendi takımının ve rakibin bölgesel performans farklarının özetini taşır.

---

## 17. MatchPrediction

Alanlar:

- `PossessionProbability`
- `ExpectedHomeGoals`
- `ExpectedAwayGoals`
- `WinProbability`
- `DrawProbability`
- `LossProbability`
- `Location`
- `EventGoals`
- `Simulation`

`Simulation` lazy olarak `M9SimulationEngine.Simulate(this)` ile hesaplanır.

M9 prediction modeli ile event-goal/simulation katmanları aynı üst-level prediction nesnesi altında birleştirilir.

---

## 18. FinalMatchPlan

Alanlar:

- `Formation`
- `Lineup`
- `Rating`
- `Matchup`
- `TacticalScore`

Bu model pipeline'ın final lineup/plan temsilidir.

M10 ve M11 seçim zincirinin sonunda frontend'e taşınır.

---

## 19. Candidate DB modeli

Dosya:

`HattrickAI_V5/Core/CandidateEvaluationDatabase.cs`

### CandidateEvaluationDatabase

Sabitler:

```text
DefaultCapacity = 100
MaxPerFormation = 30
MinimumPerFormation = 12
```

Ana property'ler:

- `Name`
- `Capacity`
- `RequiredFormations`
- `Records`
- `Count`
- `FormationCounts`

DB kalıcı bir ML database değildir.

Kod açıklamasında açıkça analiz oturumu içindeki aday değerlendirmelerini sınırlayan ve M6/M10/M11 arasında kullanılan bir search pool olarak tanımlanır.

### CandidateEvaluationRecord

Alanlar:

- `CandidateId`
- `Formation`
- `Lineup`
- `M5SuitabilityScore`
- `M5StructuralScore`
- `TacticalScore`
- `Rating`
- `Advanced`
- `Chance`
- `Prediction`
- `RankingScore`
- `Stage`

`Stage` production pipeline'da `M6-A` veya `M6-B` olarak kullanılır.

### CandidateDatabaseSet

İki ayrı search pool taşır:

```text
FirstPass  -> Candidate Database #1
SecondPass -> Candidate Database #2
```

---

## 20. Analysis output modeli

`Analysis` modeli `Models.cs` içindedir.

Temel alanlar:

- `Build`
- `TeamName`
- `OpponentName`
- `MatchTitle`
- `Own`
- `Opponent`
- `OwnRating`
- `OpponentRating`
- `AppliedQuestionnaire`

Ayrıca pipeline sonuçlarının frontend'e aktarımı için:

- `M7Scenario`
- `M72Scenario`
- `M8Chance`
- `M9Prediction`
- `M10Decision`
- `MotorPipeline`

alanları bulunur.

`MotorPipeline` JSON'dan ignore edilir; frontend'e expose edilen diğer ilgili alanlar `AnalysisService` tarafından pipeline'dan doldurulur.

---

## 21. Veri akışı: gerçek production path

```text
CHPP
 |
 +--> teamdetails
 +--> training
 +--> players
 +--> matches
 +--> opponent matches
 +--> opponent matchlineup
 +--> opponent matchdetails
 |
 v
AnalysisService
 |
 +--> own Player[]
 +--> OpponentMatchProfile
 +--> MatchQuestionnaire
 +--> RatingContext
 |
 v
MatchDataContext
 |
 v
M3 -> PlayerAnalysisResult
 |
 v
M4 -> FormationCandidateSet
 |
 v
M5 -> PositionAssignmentCandidate[]
 |
 v
M6-A
 |
 +--> TacticalCandidate
 +--> RegionalRatingSnapshot
 +--> AdvancedTacticalScenarioResult
 +--> M8ChanceResult
 +--> MatchPrediction
 |
 v
Candidate DB #1
 |
 v
M10 decision
 |
 v
M6-B
 |
 v
Candidate DB #2
 |
 v
M11
 |
 +--> FinalMatchPlan
 +--> FinalPrediction
 |
 v
Analysis
 |
 v
Frontend
```

---

## 22. Kimlik ve determinism

Aday modellerinde deterministik kimlikler kullanılmaktadır.

Özellikle lineup candidate identity:

```text
PositionCode:PlayerId
```

çiftlerinin sıralı birleşimi ile oluşturulur.

Candidate DB kayıtları `CandidateId` üzerinden benzersiz tutulur.

Aynı candidate tekrar eklenirse daha yüksek `RankingScore` taşıyan kayıt korunabilir; daha düşük veya eşit skor yeni kayıt olarak kabul edilmez.

Bu yapı M6 search, DB1/DB2 ve deterministic rerun testleri açısından önemlidir.

---

## 23. Veri modelindeki kritik ayrımlar

### TeamTactic ≠ TeamAttitude

`TeamTactic` taktik modelinin inputudur.

`TeamAttitude` maç yaklaşımıdır.

Bu iki alan UI veya teknik dokümanda birbirinin yerine kullanılmamalıdır.

### DB1/DB2 ≠ kalıcı database

Candidate DB'ler kalıcı öğrenme deposu değildir; tek analiz akışındaki search/ranking havuzlarıdır.

### Lineup ≠ final karar

`Lineup` yalnızca belirli bir XI/pozisyon düzenini temsil eder.

Final seçimi M10/M11 katmanında gerçekleşir.

### Rating ≠ prediction

`RegionalRatingSnapshot` bölgesel takım ratinglerini taşır.

`MatchPrediction` ise possession/xG/W-D-L gibi maç sonucuna yönelik prediction çıktısını taşır.

---

## 24. Bilinen veri modeli riskleri / açıklar

1. `Lineup.DisplaySlots` duplicate code'ları presentation seviyesinde normalize eder. Bu davranışın gerçek engine semantics ile karıştırılmaması gerekir.
2. `CandidateEvaluationDatabase` kapasitesi ile frontend'e expose edilen formation-diversified subset aynı şey değildir.
3. `OpponentMatchProfile` tarihsel rakip snapshot'ı temsil eder; current/future opponent state olarak yorumlanmamalıdır.
4. `MatchPrediction.Simulation` lazy hesaplandığı için prediction nesnesinin her erişiminde bağımsız bir persisted simulation kaydı oluştuğu varsayılmamalıdır.
5. `MotorPipelineResult` içinde bazı veri alanları cache/evaluation zincirinden türetilir; bunların doğrudan CHPP verisi olduğu varsayılmamalıdır.
6. `TeamTactic.Normal` production web path'te input olarak gelir; veri modelinde bu değer "motor tarafından seçilmiş optimum taktik" olarak etiketlenmemelidir.

---

## 25. Kaynak dosyalar

- `HattrickAI_V5/Core/Models.cs`
- `HattrickAI_V5/Core/MotorPipelineContracts.cs`
- `HattrickAI_V5/Core/QuestionnaireContext.cs`
- `HattrickAI_V5/Core/OpponentMatchProfile.cs`
- `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs`
- `HattrickAI_V5/Core/AnalysisService.cs`
- `HattrickAI_V5/Core/PlayerAnalysisEngine.cs`
- `HattrickAI_V5/Core/FormationCandidateEngine.cs`
- `HattrickAI_V5/Core/PositionOptimizationEngine.cs`
- `HattrickAI_V5/Core/CandidateEvaluationDatabase.cs`
