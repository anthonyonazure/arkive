# Story 8.3: Report Export — PDF & Shareable Link

## Status: done

## Story
As an MSP Tech (Mike),
I want to export QBR reports in formats suitable for client presentations,
So that I can share savings data with client decision makers.

## Acceptance Criteria
- [x] "Export PDF" generates and downloads a PDF with cover page (tenant name, date), savings summary, breakdown tables, Arkive branding in footer
- [x] "Share Link" generates a shareable read-only link (30-day expiry, no auth), copied to clipboard with toast
- [x] "Print" opens browser print dialog with report formatted for printing with page breaks and margins

## Tasks
- [x] Frontend: Create report PDF generator using jsPDF + autoTable
- [x] Backend: Create ReportSnapshot entity and migration for shareable links
- [x] Backend: Create API endpoint to create report snapshot (POST /v1/reports/snapshots)
- [x] Backend: Create public endpoint to serve snapshot (GET /v1/reports/snapshots/{token})
- [x] Frontend: Add Share Link button with clipboard copy + toast
- [x] Frontend: Add Export PDF and Print buttons to ReportDialog
- [x] Frontend: Add print CSS styles to ReportPreview
- [x] Frontend: Create public shared report page at /shared/[token]

## Dev Agent Record

### File List
- src/arkive-web/src/lib/report-export.ts (new) — PDF generator using jsPDF + autoTable
- src/Arkive.Core/Models/ReportSnapshot.cs (new) — Report snapshot entity for shareable links
- src/Arkive.Data/Configurations/ReportSnapshotConfig.cs (new) — EF Core config with unique index on Token
- src/Arkive.Data/ArkiveDbContext.cs (modified) — Added ReportSnapshots DbSet
- src/Arkive.Core/DTOs/ReportSnapshotDto.cs (new) — DTOs for create/response/shared report data
- src/Arkive.Functions/Api/ReportEndpoints.cs (new) — POST create snapshot + GET public shared report
- src/Arkive.Functions/Middleware/AuthenticationMiddleware.cs (modified) — Added GetSharedReport to anonymous endpoints
- src/arkive-web/src/components/reports/report-dialog.tsx (modified) — Added PDF/Share/Print buttons
- src/arkive-web/src/app/globals.css (modified) — Added print media query styles
- src/arkive-web/src/app/shared/[token]/page.tsx (new) — Public shared report page

### Change Log
- 2026-02-02: Story created
- 2026-02-02: Implemented PDF generator with cover page, savings summary, trend table, top sites, storage breakdown, recommendations
- 2026-02-02: Created ReportSnapshot entity, EF config, DTOs, and API endpoints
- 2026-02-02: Added Export PDF, Share Link, Print buttons to ReportDialog
- 2026-02-02: Created public shared report page at /shared/[token]
- 2026-02-02: Code review — added GetSharedReport to anonymous endpoints (#2), removed duplicated utils (#3), fixed JSON camelCase serialization (#4)
