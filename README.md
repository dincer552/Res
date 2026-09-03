# HattrickAI V5

## V5 GÜNCEL ÇALIŞMA DURUMU — 2026-09-03

Aktif branch: `v5`

V5 canlı analizde M3 → M11 çoklu aday zincirine sahiptir. Ana hedef; tüm legal formasyonları yeterli search derinliğiyle yarıştırmak ve M7 → M7.2 → M8 → M9 hattını gerçek Hattrick maç motoru davranışına mümkün olduğunca yaklaştırmaktır.

### Güncel motor zinciri

```text
M3    Oyuncu Analizi
 ↓
M4    Formasyon Üretimi
 ↓
M5    11 Adayı Üretimi
 ↓
M6-A  Global Arama / formation-aware search
 ↓
DB1   Candidate Database #1
 ↓
M7    Bölgesel Rating
 ↓
M7.2  Taktik Senaryo
 ↓
M8    Şans / Eşleşme
 ↓
M9    Maç Tahmini + W/D/L + Monte Carlo
 ↓
M10   Formasyon Kararı / Competition
 ↓
M6-B  İkinci Arama / Refinement + Exploration
 ↓
DB2   Candidate Database #2
 ↓
M11   Final Seçici
 ↓
WEB   Final XI + Individual Order
```

## MATCH ENGINE ARAŞTIRMA DURUMU

2026 Hattrick araştırma makalesi M8/M9 için ana araştırma referansı olarak kullanılmaktadır. Makale çok geniş gerçek maç verisi üzerinde Bayesian-network ve probabilistic model çalışması yaparak midfield → chance allocation, sector distribution, tactics, set-pieces ve special events için ölçülmüş/modelleştirilmiş ilişkiler sunmaktadır.

Kaynak-derived mekanizmalar production'a alınırken tek fixture'a göre değil, makaledeki mekanizma + kendi CHPP historical dataset karşılaştırması ile karar verilir.

### Production'a alınmış PDF mekanizmaları

```text
PDF Eq.1  midfield → possession/chance ownership       ✅
PDF Eq.2  5 exclusive + 5 shared discrete allocation   ✅
PDF Eq.3  L/M/R + set-piece baseline                   ✅
PDF Eq.4  L/M/R attack vs defence scoring              ✅
```

Makalenin Eq.3 baseline'ı:

```text
Left     25.65%
Centre   36.15%
Right    25.65%
DFK       5.86%
IFK       4.18%
PK        2.51%
```

60 CHPP maçlık Phase D dataset'inde gözlenen L/M/R toplamı maç başına 8.80 iken PDF Eq.2 + Eq.3 mekanizması 10 × 0.8745 = 8.745 verir. Fark yalnızca 0.055 şans/maçtır.

PDF Eq.1 ayrıca 60 maçlık ownership testinde önceki midfield-share ve regression yaklaşımlarından daha düşük hata vermiştir; bu nedenle production M8 ownership çekirdeği PDF Eq.1'e bağlanmıştır.

### PHASE D — Historical chance volume calibration ✅

60 CHPP maçlık dataset:

```text
samples: 60
observed L/M/R chances: 528
mean L/M/R chances: 8.80
```

Kontrol:

```text
PDF: 8.745
CHPP: 8.80
Δ: +0.055
```

Phase D sonucu: PDF chance-volume yapısı kendi historical datasetimizle uyumlu bulundu. Production M8'de bağımsız 8.8 regression sabiti kullanılmıyor; PDF mekanizması esas alınıyor.

### PHASE E — Historical sector distribution calibration 🔜 AKTİF

Amaç artık toplam chance sayısından sonra **şansların hangi sektöre düştüğünü** ölçmek.

Kaynak baseline:

```text
PDF Eq.3
L 25.65%
C 36.15%
R 25.65%
```

60 maç gözlemi:

```text
L 25.68%
C 42.81%
R 31.51%
```

Bu fark nedeniyle 60 maç oranları henüz production baseline'ın yerine geçirilmemektedir. Phase E'de veri; Normal/AiM/AoW/CA/LS/Pressing gibi tactic gruplarına ayrılarak değerlendirilecektir.

Yeni calibration katmanı:

```text
M8HistoricalChanceSample
        ↓
M8ChanceCalibrationAnalyzer
        ├── total chance error
        ├── ownership error
        └── sector calibration
             ├── Left
             ├── Centre
             └── Right
```

Her tactic grubu için ayrı sector gözlemi tutulmaktadır. Amaç önce hangi farkın genel örneklem farkı, hangisinin tactic kaynaklı olduğunu ayırmaktır.

### PHASE F — AiM / AoW full tactic conversion 🔜 SONRAKİ

PDF'nin doğrudan verdiği dönüşüm aralıkları Phase F'nin temelidir:

```text
AiM: wing → centre   20–35%
AoW: centre → wings  34–52%
```

Tactic skill ile conversion eğrisinin production uygulaması Phase F'de yapılacaktır.

### Diğer PDF mekanizmaları — henüz uygulanmadı

```text
Pressing          5–41% normal attack suppression      🔜
CA                4–45% missed→counter conversion     🔜
Long Shots        6–43% LMR→LS conversion              🔜
Play Creatively   special-event multiplier               🔜
Special Events    player/team event engine               🔜
Set-piece scoring ISP regression                         🔜
```

Pressing, örneğin, hem tek takım PS hem iki takım PS durumlarında normal attack suppression uygular; PC ise normal chance dağılımından ayrı bir special-event katmanıdır. Bu nedenle bunlar sector baseline'a tek katsayı olarak eklenmeyecektir.

## Production kuralı

```text
PDF / documented mechanism
        +
real CHPP data
        ↓
error comparison
        ↓
regression test
        ↓
production coefficient
```

Tek fixture veya küçük bir alt örnek tek başına production katsayısı belirlemeyecektir.

## Formation competition

M6-A → DB1 → M10 → M6-B → DB2 → M11 formation competition yapısı korunmaktadır. Maç motoru revizyonları bu arama mimarisini bozmayacak şekilde M7 → M8 → M9 hattında uygulanacaktır.
