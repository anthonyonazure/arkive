# Story 6.3: File Retrieval from Archive Tier with Rehydration

## Status: done

## Story
As an MSP Tech (Mike),
I want to retrieve archived files from Archive tier with appropriate rehydration handling,
So that I can restore files even from the cheapest storage tier with clear time expectations.

## Acceptance Criteria
- [x] POST endpoint handles Archive-tier files by initiating blob rehydration instead of direct download
- [x] Rehydration sets blob access tier to Cool and polls until accessible
- [x] Retrieval operation tracks rehydration status (Rehydrating → Retrieving → Completed)
- [x] GET retrieval-jobs endpoint shows rehydration progress with estimated completion
- [x] Frontend shows archive-tier warning with 4-6 hour estimate in confirmation dialog
- [x] Failed rehydration retries up to 3 times before marking as failed

## Tasks
- [x] Backend: Add Durable Functions orchestrator for rehydration polling (RehydrationOrchestrator + RehydrationActivities)
- [x] Backend: Update TriggerRetrieval to route Archive-tier files to rehydration flow
- [x] Backend: Add rehydration activities (InitiateRehydration, CheckRehydrationStatus, RetrieveRehydratedFile, UpdateOperationStatus)
- [x] Frontend: Update retrieval page confirmation dialog with archive-tier rehydration warning

## Dev Agent Record

### File List
- `src/Arkive.Functions/Orchestrators/RehydrationOrchestrator.cs` — New. Durable Functions orchestrator: 3 retry attempts with exponential backoff, 30-min polling via durable timers (max 16h), routes to RetrieveRehydratedFile on completion
- `src/Arkive.Functions/Orchestrators/RehydrationActivities.cs` — New. Activity functions: InitiateRehydration (Archive→Cool tier set), CheckRehydrationStatus (polls tier), RetrieveRehydratedFile (calls RetrieveFileAsync), UpdateOperationStatus (failure tracking)
- `src/Arkive.Functions/Api/RetrievalEndpoints.cs` — Modified. TriggerRetrieval now routes Archive-tier files to RehydrationOrchestrator via DurableTaskClient, creates "Rehydrating" operation records
- `src/arkive-web/src/app/(dashboard)/retrieval/page.tsx` — Modified. Confirmation dialog shows archive-tier rehydration warning with 4-6 hour estimate

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Full implementation complete — orchestrator, activities, endpoint routing, frontend warning
- 2026-02-02: Code review — 1 HIGH, 4 MEDIUM, 1 LOW findings. Fixing HIGH and MEDIUM:
  - #1 [HIGH] Story file updated with completed tasks and file list
  - #2 [MEDIUM] Added "Retrieving" status transition before RetrieveRehydratedFile call
  - #3 [MEDIUM] UpdateOperationStatus now queries by OperationId instead of fragile FileMetadataId lookup
  - #4 [MEDIUM] Added duplicate orchestration protection with status check before scheduling
  - #5 [MEDIUM] Changed TargetTier from "Archive" to "Cool" for rehydration operations
  - #6 [LOW] Frontend retrieval jobs panel — deferred to Story 6.4
