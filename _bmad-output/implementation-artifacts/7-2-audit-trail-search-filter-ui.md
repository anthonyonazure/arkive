# Story 7.2: Audit Trail Search & Filter UI

## Status: done

## Story
As an MSP Tech (Mike),
I want to search and filter the audit trail by tenant, date range, action type, or actor,
So that I can quickly find specific operational records.

## Acceptance Criteria
- [x] Filterable data table displayed with columns: timestamp, tenant name, actor, action, summary
- [x] Filter controls available: tenant dropdown, date range picker, action type dropdown, actor search
- [x] Table loads with the most recent 50 entries by default
- [x] Table updates to show matching entries when filters are applied
- [x] Summary card above the table shows the result count
- [x] Clicking an audit entry row shows full Details JSON in a readable format
- [x] For file operations, chain-of-custody information is shown: source → destination, approver, timestamps

## Tasks
- [x] Frontend: Add AuditEntry, AuditSearchResult, AuditFilters types to tenant.ts
- [x] Frontend: Create useAuditTrail hook in use-fleet.ts
- [x] Frontend: Create AuditDetailDialog component for entry detail view
- [x] Frontend: Rewrite /audit page with filters, table, pagination, and detail dialog

## Dev Agent Record

### File List
- `src/arkive-web/src/types/tenant.ts` (modified) — Added AuditEntry, AuditSearchResult, AuditFilters types
- `src/arkive-web/src/hooks/use-fleet.ts` (modified) — Added useAuditTrail hook
- `src/arkive-web/src/components/audit/audit-detail-dialog.tsx` (new) — Audit entry detail dialog with chain-of-custody display
- `src/arkive-web/src/app/(dashboard)/audit/page.tsx` (modified) — Full audit trail page with filters, table, pagination

### Change Log
- 2026-02-02: Story created, implementation complete
- 2026-02-02: All builds pass, ESLint clean, 120 tests pass
- 2026-02-02: Code review fix: Added operationId to chain-of-custody display
- 2026-02-02: Code review fix: Added error state handling to audit page
- 2026-02-02: Code review fix: Story file ACs checked, status → done
