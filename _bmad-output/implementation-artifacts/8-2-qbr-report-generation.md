# Story 8.2: QBR Report Generation & Preview

## Status: done

## Story
As an MSP Tech (Mike),
I want to generate a professional QBR-ready savings report for a specific tenant,
So that I can present cost savings data to clients and look like a strategic advisor.

## Acceptance Criteria
- [x] "Generate Report" button on tenant detail panel opens ReportPreview
- [x] ReportPreview renders within 30 seconds showing: savings summary, month-over-month trend chart, top archivable sites, storage breakdown by category, recommendations
- [x] Professional styling: Arkive branding subtle, client data prominent, advisor language
- [x] Month-over-month line chart when 2+ months of data exist, with current vs previous delta

## Tasks
- [x] Frontend: Create ReportPreview component with savings summary section
- [x] Frontend: Add trend chart using recharts (or lightweight chart)
- [x] Frontend: Add top archivable sites table and storage breakdown
- [x] Frontend: Add recommendations section with advisor language
- [x] Frontend: Add "Generate Report" button to tenant detail page
- [x] Frontend: Wire data fetching (analytics + trends) into ReportPreview

## Dev Agent Record

### File List
- src/arkive-web/src/components/reports/report-preview.tsx (new) — ReportPreview with SavingsSummary, TrendChart, TopArchivableSites, StorageBreakdown, Recommendations
- src/arkive-web/src/components/reports/report-dialog.tsx (new) — Dialog wrapper with lazy data loading and Generate Report button
- src/arkive-web/src/app/(dashboard)/fleet/[tenantId]/page.tsx (modified) — Added ReportDialog import and placement in tenant header
- src/arkive-web/package.json (modified) — Added recharts dependency

### Change Log
- 2026-02-02: Story created
- 2026-02-02: Implemented ReportPreview component with all sub-sections (savings summary, trend chart, top sites, storage breakdown, recommendations)
- 2026-02-02: Created ReportDialog with lazy data fetching and skeleton loading
- 2026-02-02: Wired Generate Report button into tenant detail page header
- 2026-02-02: Code review — fixed duplicate data fetching (#2), weighted stale percentage (#3), negative remaining potential guard (#4)
