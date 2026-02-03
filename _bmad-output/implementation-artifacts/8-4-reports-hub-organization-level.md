# Story 8.4: Reports Hub — Organization-Level Reporting

## Status: done

## Story
As an MSP Admin (Rachel),
I want to view organization-level ROI reports across all tenants,
So that I can track the business value of Arkive and present it to stakeholders.

## Acceptance Criteria
- [x] /reports shows org-wide metrics: total tenants, total storage managed, total savings achieved, total savings potential
- [x] Tenant-level breakdown table with per-tenant savings and trends
- [x] Click a tenant in reports view can generate and preview QBR report (reuses Story 8.2)
- [x] "Generate Org Report" produces aggregate report: all-tenant savings, per-tenant breakdown, projections, Arkive cost vs savings

## Tasks
- [x] Frontend: Replace reports page placeholder with org-level dashboard
- [x] Frontend: Add org-wide metric cards (tenants, storage, savings)
- [x] Frontend: Add tenant breakdown table with savings columns
- [x] Frontend: Add per-tenant ReportDialog integration
- [x] Frontend: Create org-level report preview component
- [x] Frontend: Add "Generate Org Report" button with PDF export

## Dev Agent Record

### File List
- src/arkive-web/src/app/(dashboard)/reports/page.tsx (modified) — Replaced placeholder with full reports hub
- src/arkive-web/src/components/reports/org-report-preview.tsx (new) — Org-level report preview with portfolio metrics, tenant breakdown, projections
- src/arkive-web/src/lib/org-report-export.ts (new) — Org-level PDF export using jsPDF + autoTable
- src/arkive-web/src/components/reports/report-dialog.tsx (modified) — Added controlled mode support (open/onOpenChange props)

### Change Log
- 2026-02-02: Story created
- 2026-02-02: Replaced reports page placeholder with full org-level hub (metric cards, tenant table, QBR integration)
- 2026-02-02: Created OrgReportPreview component with portfolio overview, tenant breakdown, projections
- 2026-02-02: Created org-level PDF export
- 2026-02-02: Added controlled mode to ReportDialog for external open/close
- 2026-02-02: Code review — added DialogDescription for accessibility (#3)
