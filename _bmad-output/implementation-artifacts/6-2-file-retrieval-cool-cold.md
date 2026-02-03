# Story 6.2: File Retrieval from Cool & Cold Tiers

## Status: done

## Story
As an MSP Tech (Mike),
I want to retrieve archived files from Cool or Cold storage back to SharePoint,
So that clients can access files they need within minutes.

## Acceptance Criteria
- [x] POST endpoint accepts file IDs and target SharePoint location for retrieval
- [x] Retrieval downloads from Azure Blob and uploads back to SharePoint via Graph API
- [x] Retrieval operation status tracked in ArchiveOperations with Action="Retrieve"
- [x] FileMetadata ArchiveStatus updated to "Retrieved" on completion
- [x] GET endpoint returns list of recent retrieval operations for a tenant
- [x] Frontend has retrieval trigger from search results with confirmation

## Tasks
- [x] Backend: Create retrieval request/response DTOs
- [x] Backend: Create POST trigger-retrieval and GET retrieval-status endpoints
- [x] Backend: Implement RetrieveFileAsync in ArchiveService
- [x] Frontend: Add retrieval types, hooks, and trigger flow from search results

## Dev Agent Record

### File List
- `src/Arkive.Core/DTOs/RetrievalDto.cs` — Created RetrievalRequest, RetrievalOperationDto, RetrievalBatchResult
- `src/Arkive.Core/Interfaces/IArchiveService.cs` — Added RetrieveFileAsync method and RetrieveFileInput class
- `src/Arkive.Functions/Services/ArchiveService.cs` — Implemented RetrieveFileAsync: blob download, Graph API upload, status tracking
- `src/Arkive.Functions/Api/RetrievalEndpoints.cs` — Added IArchiveService dependency, TriggerRetrieval POST endpoint, GetRetrievalJobs GET endpoint
- `src/arkive-web/src/types/tenant.ts` — Added RetrievalRequest, RetrievalOperation, RetrievalBatchResult interfaces
- `src/arkive-web/src/hooks/use-fleet.ts` — Added useTriggerRetrieval and useRetrievalJobs hooks
- `src/arkive-web/src/app/(dashboard)/retrieval/page.tsx` — Added file selection checkboxes, retrieve button, confirmation dialog with archive-tier warning

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Full implementation complete — backend DTOs, service method, endpoints, frontend types, hooks, and UI
- 2026-02-02: Code review — 1 HIGH, 4 MEDIUM, 1 LOW findings. All HIGH and MEDIUM fixed:
  - #1 [HIGH] Story file updated with completed tasks and file list
  - #2 [MEDIUM] Reduced synchronous batch limit from 50 to 10 for Cool/Cold retrieval
  - #3 [MEDIUM] Removed unused TargetSiteId from RetrievalRequest DTO
  - #4 [MEDIUM] Added 250MB size guard in RetrieveFileAsync for memory safety
  - #5 [MEDIUM] Replaced AlertDialogAction with Button to prevent premature dialog close
  - #6 [LOW] useRetrievalJobs no enabled guard — deferred (no consumer yet)
- 2026-02-02: All fixes verified — backend 0 errors, frontend build/lint clean, 120 tests pass. Story complete.
