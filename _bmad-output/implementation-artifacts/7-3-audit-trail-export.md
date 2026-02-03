# Story 7.3: Audit Trail Export

## Status: done

## Story
As an MSP Admin (Rachel),
I want to export audit trail data for compliance reporting,
So that I can provide documentation to auditors and clients.

## Acceptance Criteria
- [x] Export format options are available: CSV and PDF
- [x] CSV file is downloaded with all audit entries matching the current filters, including columns: timestamp, tenant, actor, action, details
- [x] PDF is generated with: report header (org name, date range, filters applied), audit entries table, chain-of-custody summaries for file operations
- [x] Export completes within 30 seconds

## Tasks
- [x] Backend: Create GET /v1/audit/export endpoint returning CSV with all filtered entries (up to 10k)
- [x] Frontend: Install jspdf + jspdf-autotable for PDF generation
- [x] Frontend: Create audit-export.ts utility with CSV download and PDF generation functions
- [x] Frontend: Add useAuditExportCsv hook for server-side CSV export
- [x] Frontend: Add useAuditExportAll hook for PDF data fetching
- [x] Frontend: Add CSV and PDF export buttons to audit trail page
- [x] Code review fix: CSV export uses authenticated apiClient.getRaw instead of raw fetch
- [x] Code review fix: PDF export fetches entries via useAuditExportAll before generating
- [x] Code review fix: UTF-8 BOM added to CSV export for Excel compatibility

## Dev Agent Record

### File List
- `src/Arkive.Functions/Api/AuditEndpoints.cs` (modified) — Added ExportAuditEntries CSV endpoint with CsvEscape helper, UTF-8 BOM prefix
- `src/arkive-web/src/lib/audit-export.ts` (new) — downloadCsvBlob and generateAuditPdf utilities
- `src/arkive-web/src/lib/api-client.ts` (modified) — Added requestRaw function and apiClient.getRaw method for non-JSON responses
- `src/arkive-web/src/hooks/use-fleet.ts` (modified) — Added useAuditExportCsv and useAuditExportAll mutation hooks
- `src/arkive-web/src/app/(dashboard)/audit/page.tsx` (modified) — Added CSV/PDF export buttons and handlers

### Change Log
- 2026-02-02: Story created, implementation complete
- 2026-02-02: Backend CSV export endpoint, client-side PDF generation with jspdf
- 2026-02-02: Code review fixes — auth token on CSV export, PDF fetches all entries, UTF-8 BOM for Excel
- 2026-02-02: All builds pass, ESLint clean, 120 tests pass
