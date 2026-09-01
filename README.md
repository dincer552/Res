# HattrickAI V5

## M3-M6 STABILIZATION ACTION PLAN — 2026-09-01

M8'e kadar yapılan genel analiz sonucunda, yeni özellik eklemekten önce aşağıdaki dört aşama sırayla stabilize edilecektir. Amaç M7/M8'i yanlış veya eksik adaylar üzerine kurmamak ve her katmanda deterministik, izlenebilir veri üretmektir.

### 1. M3 — Oyuncu / Regional Rating temeli 🔴

- 7 bölgesel rating deterministik olmalı.
- Oyuncu → pozisyon katkısı, Individual Order, form, loyalty, experience ve stamina kontrol edilmeli.
- Overcrowding ve home/away doğru katmanda kalmalı.
- Team Spirit / attitude oyuncu temel hesabına yanlışlıkla gömülmemeli.
- Aynı input her çalıştırmada aynı ratingi vermeli.

**Kural:** M3 temel katsayıları yeni gerçek maç verisiyle doğrulanmadan değiştirilmemeli.

### 2. M4 — Legal role / behaviour adayları 🔴

M4 oyuncu için legal pozisyon/rol ve Individual Behaviour adaylarını eksiksiz üretmeli.

Kontrol: GK, Central Defender, Wing Back, Inner Midfielder, Winger, Forward ile Normal, Offensive, Defensive, Towards Middle, Towards Wing ve pozisyona bağlı diğer legal seçenekler.

**Hedef:** Illegal candidate = 0, legal seçenek kaybı = 0. M4 nihai davranış seçmez; aday üretir.

### 3. M5 — XI Candidate üretimi 🔴

M5'in çıktısı M6'nın gerçek aday havuzu olacak.

Kontrol:
- 11/11 slot doluluğu
- aynı oyuncunun aynı XI içinde iki kez kullanılmaması
- eligibility
- formation/slot uyumu
- M3 suitability kaybı olmaması
- M4 role/behaviour bilgilerinin kaybolmaması
- CandidateId/FormationId/LineupId izlenebilirliği
- alternatif XI'ların gereksiz erken elenmemesi

Exact Hungarian assignment korunacak; alternatif adaylar geniş aday/beam yaklaşımıyla tutulacak.

**Hedef:** M5 → M6 veri kaybı = 0.

### 4. M6 — Global XI + Behaviour optimizasyon döngüsü 🔴

M6 tek bir oyuncu/behaviour skoruna göre karar vermemeli. M5 XI adayları ve legal behaviour adayları gerçek rating/matchup zincirinde değerlendirilmelidir.

```text
M5 XI Candidate
 ↓
M4/M6 Legal Behaviour Candidates
 ↓
M3 Rating
 ↓
M7 / M7.2 Scenario
 ↓
M8 Matchup
 ↓
Candidate Score
 ↓
Best candidates retained
 ↓
Controlled iteration
```

248.832 gibi büyük uzaylar körlemesine RAM'e doldurulmamalı. Legal/basic structural pruning ve dominance kontrollerinden sonra pahalı M7/M8 değerlendirmesine geçilmeli.

**Database/learning bu aşamada kullanılmayacak.** Historical evidence ancak yeterli gerçek maç verisi oluştuğunda M9/M10 tarafında değerlendirilecek.

**Hedef:** M6'nın seçtiği XI/behaviour kombinasyonu M7→M8 ile doğrulanabilir ve izlenebilir olmalı.

## M3-M6 ortak regression kapısı

Aynı offline CHPP fixture üzerinde tam zincir:

```text
M3 → M4 → M5 → M6
```

Her aşama şunları raporlamalı:

- Input count
- Output count
- Invalid candidate count
- Missing data count
- Duplicate candidate count
- CandidateId continuity
- PASS / FAIL

Bir aşama FAIL olursa sonraki aşamaya geçilmeyecek; düzeltme sonrası aynı fixture tekrar çalıştırılacak.

### Sonraki sıra

```text
M3 stabilize
 ↓
M4 stabilize
 ↓
M5 stabilize
 ↓
M6 stabilize
 ↓
M7/M7.1 regression tekrar
 ↓
M7.2 regression tekrar
 ↓
M8 full matchup regression
 ↓
M9
```

Bu dört aşama tamamlanmadan M9/M10 tarafında yeni optimizasyon mantığı eklenmeyecek.

---

# Aktif geliştirme hattı

**Aktif branch: `v5`**

V5, Hattrick maç analizini tek bir büyük formül yerine birbirine bağlı, aday üreten ve gerektiğinde geri beslemeli motor mimarisiyle geliştirir.

## Nihai hedef akış — V5.1 mimari planı

```text
M1  Veri / CHPP
 │
 ├──────────────→ M2  Rakip Analizi
 │
 └──────────────→ M3  Oyuncu Analizi
                         ↓
                  M4 Diziliş Adayları
                         ↓
                  M5 XI Adayları
                         ↓
                  M6 Individual Behaviour Adayları
                         ↓
                  M6B Maç Ayarı Adayları
                         ↓
                  M7 Rating Simulation
                         ↓
                  M8 Matchup Engine
                         ↓
                  M9 Tactical Value
                         ↓
                  M10 Global Match Optimizer
                         ↓
                  M11 Prediction
```

**Not:** README'nin mevcut motor tanımları, test sonuçları, M7/M7.1 historical validation notları ve M7.2/M8 hedefleri korunmaktadır. Bu güncelleme özellikle M3-M6 için yeni çalışma sırasını ve regression kapısını ana çalışma planına ekler.
