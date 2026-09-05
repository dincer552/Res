# HattrickAI V5 — Technical Manual Source Index

**Document purpose:** This file is the source-of-truth index for the consolidated Aşama 8 technical manual.

## Update / freshness policy

The consolidated PDF is a publication artifact. Its technical content must be traceable to the Markdown documents listed below.

**PDF source snapshot date: 2026-09-05**

When the PDF is regenerated, update the snapshot date and the per-file source revision table below. The PDF must state the same snapshot date on its cover and in its source register.

A Markdown file changed after the snapshot date is **not** silently treated as represented by the older PDF. Regenerate the PDF and record a new snapshot date when a source document changes materially.

## Source documents

| Document | Scope | PDF source at snapshot |
|---|---|---|
| `PROJECT_MEMORY.md` | Project state, decisions and boundaries | 2026-09-05 |
| `ENGINE_MAP.md` | Engine/code map | 2026-09-05 |
| `CHANGE_HISTORY.md` | Technical change history | 2026-09-05 |
| `SYSTEM_ARCHITECTURE.md` | System architecture and runtime flow | 2026-09-05 |
| `DATA_MODEL.md` | Data structures and contracts | 2026-09-05 |
| `MATCH_ENGINE_MATH.md` | Match-engine mathematics/reference | 2026-09-05 |
| `MOTOR_TECHNICAL_MANUAL.md` | M3–M11 technical descriptions | 2026-09-05 |
| `REAL_MATCH_ANALYSIS.md` | Real fixture analysis | 2026-09-05 |
| `WEB_USER_MANUAL.md` | User-facing web manual | 2026-09-05 |
| `WEB_INTERFACE.md` | Web interface technical description | 2026-09-05 |
| `WEB_UI_FILE_MAP.md` | Frontend file/function map | 2026-09-05 |
| `DEVELOPER_API_MANUAL.md` | Backend/API/developer manual | 2026-09-05 |
| `M8_PHASE_D_PDF_CALIBRATION.md` | M8 PDF/calibration-specific notes | 2026-09-05 |

## Revision rule

1. Change the relevant Markdown source first.
2. Record the change in `CHANGE_HISTORY.md` when it is a project-level technical change.
3. Regenerate the consolidated PDF from the current Markdown snapshot.
4. Change the PDF snapshot date in this file.
5. Record the generated PDF filename and snapshot date in the release/publication notes.

## Important distinction

The individual `.md` files remain the maintainable technical sources. The PDF is a frozen publication snapshot. The PDF does not replace the `.md` files.

## Content discipline

Only repository-verified behavior, formulas, configuration values, fixtures and documented boundaries belong in the manual. If a value or behavior is not supported by the repository sources, it must not be presented as a V5 production fact.
