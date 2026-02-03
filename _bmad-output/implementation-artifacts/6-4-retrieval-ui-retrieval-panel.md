# Story 6.4: Retrieval UI — RetrievalPanel Component

## Status: done

## Story
As an MSP Tech (Mike),
I want a visual interface for selecting, reviewing, and tracking file retrievals,
So that the retrieval process feels safe and transparent.

## Acceptance Criteria
- [x] Page shows search bar, recent retrievals section, and active retrieval progress
- [x] Selection summary bar shows "N files selected, X.X GB total" with "Retrieve Selected" button
- [x] Cool/Cold retrieval shows file-by-file progress status
- [x] Archive-tier retrieval shows status ("Rehydrating..."), estimated completion time, files being retrieved
- [x] Completed retrieval shows "suggest rule adjustment" prompt

## Tasks
- [x] Frontend: Add RetrievalJobsPanel component showing active and recent retrieval jobs
- [x] Frontend: Add polling via useRetrievalJobs hook with auto-refresh for active jobs
- [x] Frontend: Show rehydration status with estimated completion time for archive-tier jobs
- [x] Frontend: Add "Adjust rules?" link on completed retrievals
- [x] Frontend: Integrate RetrievalJobsPanel into retrieval page

## Dev Agent Record

### File List
- `src/arkive-web/src/components/retrieval/retrieval-jobs-panel.tsx` — New. RetrievalJobsPanel component with active/recent job sections, status badges, estimated completion times, and "Adjust rules?" prompt
- `src/arkive-web/src/app/(dashboard)/retrieval/page.tsx` — Modified. Added RetrievalJobsPanel import and integration, added total size display in selection bar
- `src/arkive-web/src/hooks/use-fleet.ts` — Modified. Updated useRetrievalJobs to accept options object with polling support (refetchInterval: 15s)

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Full implementation complete — RetrievalJobsPanel component, polling hook, page integration
- 2026-02-02: Code review — 1 HIGH, 2 MEDIUM, 1 LOW findings. Fixing HIGH and MEDIUM:
  - #1 [HIGH] Story file updated with completed tasks and file list
  - #2 [MEDIUM] Added total size display in selection bar button
  - #3 [MEDIUM] Conditional polling — only poll when active jobs exist
  - #4 [LOW] Hardcoded 5h rehydration estimate — acceptable rough estimate, deferred
