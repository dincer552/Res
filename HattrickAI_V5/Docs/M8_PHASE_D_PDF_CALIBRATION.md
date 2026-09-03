# M8 PHASE D — PDF + 60 MAÇ KALİBRASYONU

Tarih: 2026-09-03

## Amaç

M8 chance üretimini tek bir continuous `StructuralChanceIndex` yerine araştırma makalesindeki discrete chance mimarisine yaklaştırmak ve bunu 60 gerçek CHPP maçıyla kontrol etmek.

## Kaynaklar

### 1. 2026 Hattrick araştırma makalesi

`Decoding the mechanisms of the Hattrick football manager game using Bayesian network structure learning`

Temel bulgular:

- Eq. 1: midfield ratinglerden non-lineer possession/allocation olasılığı:
  `(4*Mown-3)^3 / ((4*Mown-3)^3 + (4*Mopp-3)^3)`
- Eq. 2: 5 exclusive + 5 shared/open denemesi ile discrete normal chance allocation.
- Eq. 3: Normal attack dağılımı:
  - Left 25.65%
  - Centre 36.15%
  - Right 25.65%
  - DFK 5.86%
  - IFK 4.18%
  - PK 2.51%
- Eq. 4: L/M/R attack-vs-defence scoring probability:
  `0.92*(4A-3)^3.5 / (0.92*(4A-3)^3.5 + (4D-3)^3.5)`
- AiM: wing attackların yaklaşık 20–35%'i merkeze taşınır.
- AoW: centre attackların yaklaşık 34–52%'si kanatlara taşınır.
- Set-piece taker skill CHPP verisinden gözlenemediği için set-piece scoring ayrı tutulmalıdır.

## 2. Gerçek 60 maç CHPP dataset

Fixture:

`HattrickAI_V5_M8_PhaseD_Calibration_2026-09-03T11-43-27-755Z.json`

- 60 match
- 60/60 match detail başarılı
- archive unique matches: 102
- 9 x 45 günlük archive window
- ortalama own possession: 51.18%
- toplam gözlenen L/M/R sector chance: 528
- maç başına ortalama L/M/R chance: **8.80**

## Kritik doğrulama

Eq. 3'te L/M/R toplamı:

`0.2565 + 0.3615 + 0.2565 = 0.8745`

Eq. 2'nin beklenen 10 normal attack hacmi ile:

`10 * 0.8745 = 8.745`

Gerçek 60 maç:

`mean observed L/M/R = 8.80`

Fark:

`+0.055 chance / match`

Bu nedenle önceki `8.8` değeri artık bağımsız bir regression sabiti olarak değil, PDF mekanizmasının gerçek veride doğrulanan sonucu olarak yorumlanmalıdır.

60 maçta sabit 8.745 hacmin:

- MAE = 1.4765
- signed error = -0.0550

## Possession / ownership karşılaştırması

Gerçek 60 maçta gözlenen own chance share ile karşılaştırıldığında:

### Eski basit yaklaşım

`ownShare = midfieldShare`

- ownership MAE ≈ **0.1783**
- own chance MAE ≈ **1.6819**

### Önceki 60-maç regression yaklaşımı

`ownShare = clamp(-0.4380926172 + 1.9561688498 * midfieldShare)`

- ownership MAE ≈ **0.1323**
- own chance MAE ≈ **1.3598**

### PDF Eq. 1

Gerçek home/away midfield ratingleri kullanılarak:

`POS = (4*Mown-3)^3 / ((4*Mown-3)^3 + (4*Mopp-3)^3)`

- ownership MAE ≈ **0.1221**
- own chance MAE ≈ **1.2399**
- opponent chance MAE ≈ **1.3392**

Sonuç: PDF Eq. 1, mevcut iki yaklaşımın da üzerinde performans gösterdiği için production M8 possession çekirdeğine alınmıştır.

## Sector distribution kararı

60 maçta gözlenen own L/M/R dağılımı:

- Left: 25.68%
- Centre: 42.81%
- Right: 31.51%

PDF Eq. 3:

- Left: 25.65%
- Centre: 36.15%
- Right: 25.65%

60 maçlık dağılım PDF ile birebir aynı değildir. Bunun nedeni örneklemin küçük olması ve Hattrick'teki diğer match/tactic koşullarının dağılımı etkileyebilmesidir. Bu nedenle 60 maçlık oranlar PDF baseline'ın yerine geçirilmemiştir.

Production baseline olarak PDF Eq. 3 korunur; 60 maç seti validation/calibration dataset olarak kullanılmaya devam eder.

## Kod mimarisi

Yeni M8 akışı:

```text
M7 midfield ratings
       ↓
PDF Eq. 1 possession
       ↓
5 exclusive + 5 shared
       ↓
PDF Eq. 2 expected allocation
       ↓
Expected L/M/R volume = 8.745 total
       ↓
PDF Eq. 3 sector distribution
       ↓
AiM / AoW conversion
       ↓
PDF Eq. 4 attack vs defence
       ↓
M9 xG / W-D-L
```

### Production'a alınanlar

- PDF Eq. 1 possession probability
- PDF Eq. 2 discrete chance architecture
- PDF Eq. 3 L/M/R + set-piece baseline
- PDF Eq. 4 L/M/R scoring probability
- AiM/AoW transfer ranges

### Henüz production'a alınmayanlar

- Pressing suppression
- Counter Attack extra chance conversion
- Long Shots conversion
- Play Creatively special-event multiplier
- Specialty event resolution
- Set-piece taker-specific scoring
- Full event-based M9

Bunlar ayrı fazlarda kalibre edilecektir.

## M9 değişikliği

M9 artık:

- `BaseGoals = 0.20`
- `GoalScale = 2.80`
- `StructuralChancePool = 10`

gibi chance-to-goal için bağımsız/arbitrary continuous katsayılara dayanmıyor.

Normal L/M/R scoring doğrudan PDF Eq. 4 ile çözülüyor.

Set-piece conversion için şimdilik nötr `0.5` kullanılıyor; bunun sebebi araştırma makalesinin set-piece taker skill'inin gözlenemediğini açıkça belirtmesidir.

## Monte Carlo

M9 1000x senaryo tabanı da PDF Eq. 3'e hizalandı:

`36.15 / 25.65 / 25.65 / 12.55`

Monte Carlo hâlâ final event motoru değildir; tam event-based Monte Carlo daha sonraki Phase O'dur.

## Sonraki faz

```text
PHASE D  PDF + 60-match chance allocation validation   ✅
    ↓
PHASE E  Historical sector/tactic calibration          🔜
    ↓
PHASE F  AiM/AoW full tactic conversion                🔜
    ↓
PHASE G  Pressing suppression                          🔜
    ↓
PHASE H  Counter Attack                                🔜
    ↓
PHASE I  Long Shots                                    🔜
    ↓
PHASE J  Play Creatively                               🔜
    ↓
PHASE K  Specialty event engine                       🔜
    ↓
PHASE L  Specialty ↔ tactic / weather                  🔜
    ↓
PHASE M  M9 event-based resolution                    🔜
    ↓
PHASE N  Historical goal/outcome calibration           🔜
    ↓
PHASE O  Full Monte Carlo                              🔜
    ↓
PHASE P  Offline regression + real matches             🔜
```

## Production kuralı

Tek bir fixture veya küçük bir alt örnek tek başına production katsayısı belirlemeyecek.

```text
PDF / documented mechanism
        +
real CHPP data
        ↓
error comparison
        ↓
regression test
        ↓
production
```
