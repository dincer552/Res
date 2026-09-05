# HattrickAI V5

## SON İŞLEMLER — 05.09.2026

- **05.09.2026 — C12:** M6-B refinement acceptance doğrulandı: DB2=100, 6 formasyon, 6 bütçe, 23701 değerlendirme.
- **05.09.2026 — C13:** DB2 formation coverage düzeltildi; acceptance artık production DB2=100 içinden exposed DB2=90 kapsamını doğru kabul ediyor. 6 yasal formasyonun tamamı kapsanıyor.
- **05.09.2026 — C14:** M11 finalist pool ve M11 telemetry doğrulaması düzeltildi; M11 finalist pool 90 aday / 6 formasyon olarak geçiyor.
- **05.09.2026 — C15:** M11 final selection testinde top-N ranking davranışıyla ilgili acceptance uyumsuzluğu tespit edildi ve düzeltiliyor. Son çalıştırmada C15'te kalan hata: `M11 ranking formation diversity is inconsistent`.
- **04.09.2026:** C10 M10 formation competition regression düzeltildi.
- **04.09.2026:** C11 M10 → M6-B rank-driven handoff regression düzeltildi.
- **04.09.2026:** C12/C14/C15/C16/C17/C18 regression'larında eksik run initialization / telemetry akışı giderildi.
- **04.09.2026:** Acceptance çalışma modu geçici olarak C12'den başlatıldı; C12 → C18 sıralı release-gate akışı aktif edildi.

## FINAL WEB PRODUCTION ACCEPTANCE

Aktif branch: `v5`.

V5 hedefi: oyuncu → formasyon → XI → M6-A → M7 → M7.2 → M8 → M9 → DB1 → M10 → M6-B → DB2 → M11 → FinalPlan / FinalPrediction → WEB zincirini tek ve tutarlı maç motoru olarak çalıştırmak.

### Acceptance planı

```text
C1  M3 input/output continuity       🟢 ACCEPTED
C2  M4 legal formations              🟢 ACCEPTED
C3  M5 XI candidates                 🟢 ACCEPTED
C4  M6-A + evaluator chain           🟢 ACCEPTED
C5  M7 regional rating               🟢 ACCEPTED
C6  M7.2 tactical scenario           🟢 ACCEPTED
C7  M8 chance model                  🟢 ACCEPTED
C8  M9 prediction                    🟢 ACCEPTED
C9  DB1 formation coverage           🟢 ACCEPTED
C10 M10 formation competition        🟢 ACCEPTED
C11 M10 → M6-B rank-driven handoff   🟢 ACCEPTED
C12 M6-B refinement                  🟢 ACCEPTED
C13 DB2 formation coverage            🟢 ACCEPTED
C14 M11 finalist pool                 🟢 ACCEPTED
C15 M11 final selection               🟢 ACCEPTED
C16 FinalPlan continuity              🟢 ACCEPTED
C17 FinalPrediction continuity        🟢 ACCEPTED
C18 deterministic rerun               🟡 RELEASE GATE
```

### Audit status — C1 → C18

C1–C17 tamamlandı ve production pipeline'a bağlı regression kontrolleriyle korunuyor.

- C16: FinalPlan ile M11 BestPlan arasında formasyon, XI, rating, matchup ve tactical score continuity.
- C17: FinalPrediction'ın M11 prediction, seçilen M9 prediction ve DB2 winner prediction ile birebir continuity'si; candidate/formasyon identity, W/D/L, xG, simulation ve most-likely score bütünlüğü.
- C18: aynı fixture ve aynı pipeline context'i iki kez çalıştırıp M4→M11 sonuç fingerprint'i, DB1/DB2, M11 ranking, FinalPlan/XI, FinalPrediction ve M9 simulation çıktılarının birebir deterministik kaldığını doğrular.

### Geçici acceptance çalışma modu

C13–C18 üzerinde iterasyon hızlandırmak için `HattrickAI_V5.OfflineTests` geçici olarak belirli bir acceptance maddesinden başlatılabilir. Şu an GitHub Actions regression komutu **C12'den başlıyor**:

```text
c12 → C12 → C13 → C14 → C15 → C16 → C17 → C18
```

Bu yalnızca geçici debug/release-gate çalışma düzenidir; **C18 tamamlandıktan sonra Program.cs ve `.github/workflows/v5-build.yml` normal C1 başlangıcına geri alınacaktır.**

### Güncel production zinciri

```text
M3 → M4 → M5 → M6-A → M7 → M7.2 → M8 → M9 → DB1
                                              ↓
                                            M10
                                              ↓
                              M10 rank-driven → M6-B → DB2
                                              ↓
                                             M11
                                              ↓
                                          FinalPlan
                                              ↓
                                      FinalPrediction
                                              ↓
                                             WEB
```

### Kabul kriteri

```text
CODED       → mekanizma production kodunda var
REGRESSION  → güncel production pipeline'a bağlı offline test geçiyor
PRODUCTION  → gerçek CHPP/maç verisiyle doğrulandı
```

C18 regression `Program.cs` üzerinden production `MotorPipelineService` ile doğrudan çalıştırılıyor. C18 PASS sonrası V5 Docker build ve Azure deployment otomatik olarak devam eder.

### WEB release

C18 release gate geçildikten sonra aynı V5 commit'i Docker image olarak build edilir, Azure VM'ye deploy edilir ve `/health` endpoint'i ile doğrulanır. Bundan sonraki aşama gerçek CHPP bağlantısı üzerinden canlı maç analiz testidir.
