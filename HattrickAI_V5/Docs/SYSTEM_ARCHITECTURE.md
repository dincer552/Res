# Hattrick AI V5 — Sistem Mimarisi

## 1. Amaç

Bu belge V5'in mevcut repository kodundan doğrulanabilen production analiz akışını açıklar.

Kapsam yalnızca kodda gerçekten bulunan veri akışı ve motor bağlantılarıdır. Henüz doğrulanmamış davranışlar burada özellik olarak gösterilmez.

---

## 2. Production giriş noktası

Ana analiz akışı:

`HattrickAI_V5/Core/AnalysisService.cs`

`AnalysisService.RunAsync(...)` aşağıdaki bilgileri CHPP üzerinden toplar:

- takım bilgisi (`teamdetails`)
- training / self-confidence (`training`)
- kendi oyuncuları (`players`)
- seçilen yaklaşan lig maçı (`matches`)
- rakibin maç geçmişi (`matches`)
- rakibin son resmi maç kadrosu (`matchlineup`)
- rakibin son maç ratingleri (`matchdetails`)
- rakip oyuncuları (`players`)

Analiz başlamadan önce seçilen maçın geçerli, gelecekteki ve lig maçı olması kontrol edilir.

Kendi takımında en az 11 oyuncu bulunması gerekir.

---

## 3. Rakip veri hazırlama

Rakibin son tamamlanmış resmi/rekabetçi maçı bulunur.

Rakibin son maçından:

1. final sahadaki 11 oyuncu belirlenir,
2. substitutions dikkate alınır,
3. oyuncuların saha pozisyonları okunur,
4. tarihsel rating verileri alınır,
5. yıldız/pozisyon/behavior bilgileri ile rakip lineup oluşturulur.

Rakip için ayrıca `OpponentThreatEngine.Analyze(...)` çağrılır.

Bu veri `OpponentMatchProfile` içine konur ve pipeline'a aktarılır.

---

## 4. Match context oluşturulması

Production web path içinde:

- `MatchLocation` ev/deplasman durumundan belirlenir.
- `MatchImportance` questionnaire'dan alınır.
- `TeamTactic` şu anda `TeamTactic.Normal` olarak verilir.

Oluşturulan temel context:

`MatchDataContext`

Bu context kendi takım oyuncularını, rakip profilini, rating context'i, questionnaire'ı, rakip lineup'ını ve rakip oyuncularını taşır.

---

## 5. Motor pipeline

`AnalysisService` daha sonra:

`MotorPipelineService.RunAsync(context, ownPlayers, ct)`

çağrısını yapar.

Mevcut production zinciri:

```text
CHPP / Web Input
      |
      v
AnalysisService
      |
      | MatchDataContext
      v
M3 PlayerAnalysisEngine
      |
      v
M4 FormationCandidateEngine
      |
      v
M5 PositionOptimizationEngine
      |
      v
M6-A Global Behaviour Optimization
      |
      +--> M7 Regional Rating
      |
      +--> M7.2 Advanced Tactical Scenario
      |
      +--> M8 Chance / Matchup
      |
      +--> M9 Match Prediction
      |
      v
Candidate DB #1
      |
      v
M10 Final Decision / Formation Competition
      |
      v
M6-B Rank-driven Refinement
      |
      v
Candidate DB #2
      |
      v
M11 Final Selection
      |
      v
FinalPlan + FinalPrediction
      |
      v
Analysis response / Frontend
```

---

## 6. M3 — oyuncu analizi

Dosya:

`HattrickAI_V5/Core/PlayerAnalysisEngine.cs`

M3 oyuncu uygunluk profillerini üretir.

Kodun kendi açıklamasına göre M3:

- XI seçmez,
- diziliş seçmez,
- rakip skorunu kullanmaz,
- takım ratingi üretmez.

Her uygun oyuncu için tanımlı pozisyon kodlarına skor hesaplanır.

Pozisyon adayları arasında GK, DEF, W, IM ve FW slotları bulunur.

Specialty bilgisi bu aşamada doğrudan rating bonusuna çevrilmez; sonraki motorlar için profile taşınır.

---

## 7. M4 — yasal diziliş adayları

Dosya:

`HattrickAI_V5/Core/FormationCandidateEngine.cs`

M4 yalnızca yasal ve doldurulabilir diziliş adaylarını üretir.

Mevcut tek yetkili legal formation registry şu altı dizilişi içerir:

- 3-5-2
- 3-4-3
- 4-4-2
- 4-5-1
- 2-5-3
- 5-3-2

M4 oyuncu-slot feasibility kontrolü yapar ve `StructuralScore` üretir.

M4 oyuncu/slot nihai eşleştirmesini yapmaz; bu M5'e bırakılır.

---

## 8. M5 — pozisyon optimizasyonu

Dosya:

`HattrickAI_V5/Core/PositionOptimizationEngine.cs`

M5, M4'ün verdiği yasal diziliş içinde oyuncu-slot eşleştirmelerini üretir.

Kodda:

- uygun oyuncu profilleri filtrelenir,
- minimum 11 oyuncu kontrol edilir,
- slot feasibility kontrol edilir,
- tam en iyi atama Hungarian algoritması ile hesaplanır,
- alternatif adaylar beam search ile üretilir,
- adaylar `SuitabilityScore` ile sıralanır.

M5'in işi rakip değerlendirmesi, takım taktiği seçimi veya bireysel davranış seçimi değildir.

---

## 9. M6-A — global behavior optimization

Dosya:

`HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs`

M6 formation-aware search yürütür.

Her formasyon için ayrı search pass uygulanır.

M6:

- M5 XI adaylarını alır,
- baseline lineup oluşturur,
- `BehaviourCandidateEngine` ile izin verilen oyuncu davranışlarını alır,
- beam/search ile alternatif lineup'ları değerlendirir,
- evaluator üzerinden downstream M7/M7.2/M8/M9 hesaplarını tetikler,
- global best ve Candidate DB içeriğini üretir.

M6-A'da normal baseline kullanılır.

---

## 10. M7 / M7.2 / M8 / M9 evaluator zinciri

M6'nın evaluator'ı her aday lineup için downstream hesaplamayı çalıştırır.

### M7

`RegionalRatingScenarioEngine`

Lineup ve `MatchState` üzerinden bölgesel rating scenario üretir.

### M7.2

`AdvancedTacticalScenarioEngine`

M6 adayının `MatchState` içindeki takım taktiğini kullanarak advanced tactical scenario hesaplar.

Taktik burada seçilmez.

### M8

`M8ChanceModel` ve `M8ChanceAllocationEngine` üzerinden chance/matchup hesapları yapılır.

Taktik dönüşüm ve sektör şans dağılımı, verilen taktiğin sonucudur.

### M9

`M9MatchPredictionEngine` aday için maç tahmini üretir.

M6 evaluator'da ranking score:

`0.70 * TacticalScore + 0.30 * WinProbability`

şeklinde oluşturulur.

Bu ifade repository'deki mevcut pipeline kodundan alınmıştır.

---

## 11. Candidate DB #1

M6-A sırasında adaylar `CandidateDatabaseSet.FirstPass` içine kaydedilir.

Pipeline daha sonra:

`TopWithFormationDiversity(100, MaxPerFormation)`

ile DB1 adaylarını çıkarır.

DB1 içinde tüm legal formasyonların bulunması ayrıca kontrol edilir.

Bu anti-lock kontrolü formasyonlardan birinin pipeline tarafından tamamen elenmesini engellemek için kullanılır.

---

## 12. M10 — formasyon yarışması ve final karar

Dosya:

`HattrickAI_V5/Core/M10FinalDecisionEngine.cs`

M10 DB1 finalist adaylarını karşılaştırır.

M10'un mevcut pipeline içindeki rolü:

- formasyonları karşılaştırmak,
- en iyi planı seçmek,
- formation competition üretmek,
- M6-B için formation rank üretmek,
- `TeamAttitude` yaklaşımını belirlemek.

`TeamAttitude` ile `TeamTactic` aynı veri değildir.

M10'daki `SelectApproach` takım yaklaşımını seçer; bu, AttackMiddle/AttackWings gibi `TeamTactic` seçimi olarak belgelenmemelidir.

---

## 13. M6-B — rank-driven refinement

M10'un formation ranking sonucu M6-B budget'larını belirlemek için kullanılır.

Mevcut pipeline'da temel budget değerleri:

- beam width: 6
- base iterations: 3

Formation rank'e göre budget tier uygulanır.

M6-B `preserveInputOrders: true` ile çalışır ve M10 sıralamasına göre seed adaylarını kullanır.

---

## 14. Candidate DB #2

M6-B sırasında adaylar `CandidateDatabaseSet.SecondPass` içine kaydedilir.

Pipeline yine formation diversity koruyan TopWithFormationDiversity çağrısı kullanır.

DB2'nin production database count'u ile frontend'e expose edilen diversified DB2 subset'i aynı sayı olmak zorunda değildir.

Bu ayrım C13 acceptance düzeltmesinin temel nedenlerinden biridir.

---

## 15. M11 — final selection

Dosya:

`HattrickAI_V5/Core/M11FinalSelectorEngine.cs`

M11 DB2 finalist havuzunu kullanarak final planı seçer.

Pipeline sonunda seçilen lineup, ilgili DB2 kaydı ve cached evaluation üzerinden final prediction ile birleştirilir.

`MotorPipelineResult` frontend/Analysis katmanına M3-M11 sonuçlarının ilgili bölümlerini ve final plan/prediction bilgisini taşır.

---

## 16. Frontend'e dönüş

`AnalysisService` pipeline sonucundan:

- final lineup,
- final rating,
- M7 scenario,
- M7.2 scenario,
- M8 chance,
- M9 prediction,
- M10 decision,
- complete MotorPipeline

alanlarını `Analysis` sonucuna aktarır.

Frontend bu sonucu görselleştirir.

---

## 17. Mimari olarak doğrulanmış kritik ayrımlar

### Taktik seçimi

Mevcut web production path'te ayrı team-tactic selector yoktur.

Input:

`TeamTactic.Normal`

M7.2/M8 bu inputun sonuçlarını hesaplar.

### Formasyon seçimi

Formasyon aday üretimi M4'te başlar; M5 oyuncu-slot eşleştirmeleri üretir; M6 downstream evaluation yapar; M10 formasyon yarışmasını yürütür; M11 final seçimi tamamlar.

### Oyuncu davranışı

M6, `BehaviourCandidateEngine` tarafından izin verilen bireysel `PlayerOrder` seçeneklerini arar.

Bu da team tactic değildir.

---

## 18. Bilinen teknik boşluklar

Aşağıdaki konular bu belgenin doldurulmuş bölümü değildir; sonraki kaynak incelemelerinde ayrıca doğrulanmalıdır:

- M3'ün tüm edge-case davranışları
- M4 structural score'un tüm matematiksel ayrıntıları
- M5'in tüm beam-search ve Hungarian ayrıntıları
- M6'nın evaluator/cache davranışının tamamı
- DB1/DB2 sınıflarının tam veri sözleşmesi
- M9'un tüm matematiksel katsayıları
- M11'in tüm tie-break ve seçim ayrıntıları
- Frontend'in tüm response mapping ayrıntıları

Bu alanlar doğrulanmadan varsayımsal içerikle doldurulmayacaktır.

---

## Kaynak dosyalar

- `HattrickAI_V5/Core/AnalysisService.cs`
- `HattrickAI_V5/Core/MotorPipelineService.cs`
- `HattrickAI_V5/Core/PlayerAnalysisEngine.cs`
- `HattrickAI_V5/Core/FormationCandidateEngine.cs`
- `HattrickAI_V5/Core/PositionOptimizationEngine.cs`
- `HattrickAI_V5/Core/M6GlobalOptimizationEngine.cs`
- `HattrickAI_V5/Core/RegionalRatingScenarioEngine.cs`
- `HattrickAI_V5/Core/AdvancedTacticalScenarioEngine.cs`
- `HattrickAI_V5/Core/M8ChanceAllocationEngine.cs`
- `HattrickAI_V5/Core/M9MatchPredictionEngine.cs`
- `HattrickAI_V5/Core/M10FinalDecisionEngine.cs`
- `HattrickAI_V5/Core/M11FinalSelectorEngine.cs`
