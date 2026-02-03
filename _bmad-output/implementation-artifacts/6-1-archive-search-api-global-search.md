# Story 6.1: Archive Search API & Global Search Integration

## Status: done

## Story
As an MSP Tech (Mike),
I want to search for previously archived files by name, metadata, or original location,
So that I can quickly find files when a client needs them back.

## Acceptance Criteria
- [x] Search API queries FileMetadata for archived files matching: file name (partial), original site/library path, owner, file type
- [x] Results show: file name, original path, archive date, storage tier, file size, tenant name
- [x] Each result shows a tier badge and retrieval time estimate
- [x] Only files from tenants the user has access to are returned (org-scoped)
- [x] Search supports pagination with sortable results
- [x] Retrieval page has a search interface with filters and results table

## Tasks
- [x] Backend: Create ArchiveSearchDto and search request/response DTOs
- [x] Backend: Create archive search endpoint with pagination and filters
- [x] Frontend: Add search types and hooks
- [x] Frontend: Implement retrieval page with search interface and results

## Dev Agent Record

### File List
- `src/Arkive.Core/DTOs/ArchiveSearchDto.cs` — Created ArchivedFileDto, ArchiveSearchResultDto
- `src/Arkive.Functions/Api/RetrievalEndpoints.cs` — Created SearchArchivedFiles endpoint with pagination, filters, tier estimates
- `src/arkive-web/src/types/tenant.ts` — Added ArchivedFile, ArchiveSearchResult, ArchiveSearchFilters types
- `src/arkive-web/src/hooks/use-fleet.ts` — Added useArchiveSearch hook
- `src/arkive-web/src/app/(dashboard)/retrieval/page.tsx` — Implemented full search UI with table, filters, pagination

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Full implementation complete
- 2026-02-02: Code review — 1 HIGH, 3 MEDIUM, 1 LOW. All HIGH/MEDIUM fixed:
  - #1 [HIGH] Story file updated with completed tasks and file list
  - #2 [MEDIUM] Validate tenantId filter against org membership
  - #3 [MEDIUM] Fix siteNameMap dictionary key collision (GroupBy + First)
  - #4 [MEDIUM] Add minimum 2-char query length to prevent full-table scans
  - #5 [LOW] ArchivedAt uses ScannedAt — deferred (needs join to ArchiveOperations)
- 2026-02-02: All fixes verified — backend 0 errors, frontend build clean. Story complete.
