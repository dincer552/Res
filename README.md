# HattrickAI V5

## V5 ÇALIŞMA DURUMU — 2026-09-01

Aktif branch: `v5`

### Mevcut aşama

```text
M3  Oyuncu analizi                 ✅ LOCK
 ↓
M4  Formasyon / legal adaylar      ✅ VALIDATED
 ↓
M5  XI candidate sistemi           ✅ VALIDATED
 ↓
M6  Global XI + behaviour          🔴 ACTIVE ← ŞU AN BURADAYIZ
 ↓
M7  Rating integration              🟠 NEXT
 ↓
M7.1 Calibration altyapısı         🟠 NEXT
 ↓
M7.2 Taktik modeli                 🟠 NEXT
 ↓
M8  Matchup                        🟡 NEXT
 ↓
M3→M8 full regression              🟡 PENDING
 ↓
Gerçek maç calibration             🟢 PENDING
 ↓
M9                                🟢 PENDING
 ↓
M10                               🟢 PENDING
```

### Son doğrulama

- M3 stabilize edildi ve kilitli çalışma temeli olarak korunuyor.
- M4 offline fixture üzerinde legal formation/candidate kontrolleriyle doğrulandı.
- M5 revizyonları tamamlandı: eligibility, 11/11 slot, distinct player assignment, M3 suitability continuity, M4→M5 contract ve alternatif adayların erken elenmemesi korunuyor.
- Exact Hungarian assignment korunuyor; geniş aday/beam yaklaşımı sonraki adayları yaşatmak için kullanılıyor.
- Son GitHub Actions regression çalıştırmasında `M7-M8 Offline Regression` başarılı oldu; aynı workflow'da Docker build ve Azure deploy da başarılı tamamlandı.
- M5 şu an yeni matematiksel değişiklik beklemiyor; bir sonraki geliştirme odağı M6 optimizasyon döngüsü.

## M3-M6 STABILIZATION ACTION PLAN

M8'e kadar yapılan genel analiz sonucunda, yeni özellik eklemekten önce aşağıdaki dört aşama sırayla stabilize edilecektir. Amaç M7/M8'i yanlış veya eksik adaylar üzerine kurmamak ve her katmanda deterministik, izlenebilir veri üretmektir.

### 1. M3 — Oyuncu / Regional Rating temeli

- 7 bölgesel rating deterministik olmalı.
- Oyuncu → pozisyon katkısı, Individual Order, form, loyalty, experience ve stamina kontrol edilmeli.
- Overcrowding ve home/away doğru katmanda kalmalı.
- Team Spirit / attitude oyuncu temel hesabına yanlışlıkla gömülmemeli.
- Aynı input her çalıştırmada aynı ratingi vermeli.

**Kural:** M3 temel katsayıları yeni gerçek maç verisiyle doğrulanmadan değiştirilmemeli.

### 2. M4 — Legal role / behaviour adayları

M4 oyuncu için legal pozisyon/rol ve Individual Behaviour adaylarını eksiksiz üretmeli.

Kontrol: GK, Central Defender, Wing Back, Inner Midfielder, Winger, Forward ile Normal, Offensive, Defensive, Towards Middle, Towards Wing ve pozisyona bağlı diğer legal seçenekler.

**Hedef:** Illegal candidate = 0, legal seçenek kaybı = 0. M4 nihai davranış seçmez; aday üretir.

### 3. M5 — XI Candidate üretimi — VALIDATED

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

### 4. M6 — Global XI + Behaviour optimizasyon döngüsü — ACTIVE

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

**M6 mevcut teknik temel:** `BehaviourEngine` legal order matrixini üretir; `BehaviourCandidateEngine` sabit XI için slot bazlı aday matrisini ve kombinasyon sayısını üretir. Bu katman şu anda aday üretir; final winner seçimi yapmaz. Büyük Cartesian uzaylarda kör exhaustive enumeration yapılmaz.

**M6 hedefi:** M5 XI adayları + legal behaviour adayları, M7/M7.2 rating/scenario ve M8 matchup değerlendirmesine kontrollü şekilde taşınmalı; sonuç deterministik, izlenebilir ve tekrar üretilebilir olmalıdır.

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

### Güncel sonraki sıra

```text
M3 LOCK
 ↓
M4 VALIDATED
 ↓
M5 VALIDATED
 ↓
M6 ACTIVE
 ↓
M7 integration
 ↓
M7.1 calibration infrastructure
 ↓
M7.2 tactical model
 ↓
M8 matchup
 ↓
M3→M8 full regression
 ↓
Gerçek maç calibration
 ↓
M9
 ↓
M10
```

Bu dört aşama tamamlanmadan gerçek maç calibration ve sonraki global optimizasyon katmanlarına geçilmeyecek.

---

# Aktif geliştirme hattı

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

**Not:** Nihai V5.1 mimari akışı korunmaktadır. Bu README güncellemesi yalnızca mevcut çalışma durumunu ve M3→M10 ilerleme sırasını günceller; M6 tamamlanmadan M7/M8 tarafında yeni optimizasyon mantığı eklenmeyecektir.
