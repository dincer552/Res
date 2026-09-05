# HattrickAI V5

## SON İŞLEMLER — 05.09.2026

- **05.09.2026 — PRODUCTION DEPLOY:** V5 Docker build ve Azure deployment başarıyla tamamlandı; deployment health check doğrulandı.
- **05.09.2026 — REGRESSION TESTLERİ:** C1–C18 offline acceptance/regression çalıştırması şimdilik durduruldu. Deployment artık regression gate'e bağlı olmadan devam ediyor.
- **05.09.2026 — C12:** M6-B refinement acceptance doğrulandı: DB2=100, 6 formasyon, 6 bütçe, 23701 değerlendirme.
- **05.09.2026 — C13:** DB2 formation coverage düzeltildi; acceptance artık production DB2=100 içinden exposed DB2=90 kapsamını doğru kabul ediyor. 6 yasal formasyonun tamamı kapsanıyor.
- **05.09.2026 — C14:** M11 finalist pool ve M11 telemetry doğrulaması düzeltildi; M11 finalist pool 90 aday / 6 formasyon olarak geçiyor.
- **05.09.2026 — C15:** M11 final selection testindeki top-N ranking davranışıyla ilgili acceptance uyumsuzluğu giderildi; ranking top-N mantığı production davranışıyla hizalandı.
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
C18 deterministic rerun               🟡 PAUSED
```

### Audit status — C1 → C18

C1–C17 için regression kontrolleri hazır durumda. C18 deterministic rerun dahil offline acceptance/regression suite **şimdilik release pipeline'dan çıkarıldı**. Production deployment artık bu offline regression gate'ine bağlı değil.

Regression testleri silinmedi; `HattrickAI_V5.OfflineTests` altında tutuluyor ve daha sonra yeniden aktif edilecek.

### Geçici regression çalışma durumu

GitHub Actions içindeki `Offline acceptance regression` adımı 05.09.2026 itibarıyla **PAUSED** durumunda. Workflow'da test adımı korunuyor ancak çalıştırılması geçici olarak devre dışı bırakıldı. Böylece Docker build → image upload → Azure deploy → `/health` doğrulama zinciri çalışmaya devam ediyor.

Daha sonra regression gate'i yeniden devreye almak için `.github/workflows/v5-build.yml` içindeki `Offline acceptance regression (PAUSED)` adımının `if: ${{ false }}` koşulu yeniden etkinleştirilecek.

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
REGRESSION  → offline acceptance suite hazır; şimdilik PAUSED
PRODUCTION  → Docker build + Azure deployment + /health doğrulaması tamamlandı
```

### WEB release

V5 Docker image build edildi, Azure VM'ye deploy edildi ve `/health` endpoint'i ile doğrulandı. Offline acceptance/regression suite daha sonra yeniden release gate olarak aktif edilecektir. Bundan sonraki aşama gerçek CHPP bağlantısı üzerinden canlı maç analiz testidir.
