# Story 5.4: Veto Review Dashboard & Rule Adjustment

## Status: done

## Story
As an MSP Tech (Mike),
I want to review veto decisions and adjust rules or exclusions accordingly,
So that I can respond to site owner concerns and keep the archiving pipeline running smoothly.

## Acceptance Criteria
- [x] Affected tenants show an "attention" flag with "N vetos to review" in the TenantRow
- [x] VetoReviewCards display: file name/path, vetoer name, reason, veto date
- [x] "Accept Veto" reverts ArchiveStatus to "Active" and resolves the veto flag
- [x] "Override Veto" shows destructive confirmation, then re-queues for archiving without approval
- [x] "Exclude Library" creates exclusion rule and resolves the veto
- [x] Veto count endpoint returns count per tenant

## Tasks
- [x] Backend: Create GET endpoint for veto reviews per tenant
- [x] Backend: Create POST endpoint for accept/override/exclude veto actions
- [x] Backend: Add veto count to fleet overview response
- [x] Frontend: Add veto attention type and flag display in TenantRow
- [x] Frontend: Create VetoReviewCard component
- [x] Frontend: Create VetoReviewPanel in tenant detail page
- [x] Frontend: Add veto action hooks and API integration

## Dev Agent Record

### File List
- `src/Arkive.Core/DTOs/FleetOverviewDto.cs` — Added VetoCount to FleetTenantDto
- `src/Arkive.Core/DTOs/VetoReviewDto.cs` — Created VetoReviewDto, VetoActionRequest, VetoActionResult
- `src/Arkive.Functions/Services/FleetAnalyticsService.cs` — Added veto count query, ClassifyAttention veto-review type, AttentionSortOrder
- `src/Arkive.Functions/Api/VetoReviewEndpoints.cs` — Created GetVetoReviews and ResolveVeto endpoints with accept/override/exclude handlers
- `src/arkive-web/src/types/tenant.ts` — Added AttentionType "veto-review", vetoCount, VetoReview, VetoActionRequest, VetoActionResult
- `src/arkive-web/src/components/fleet/tenant-row.tsx` — Added veto badge (compact) and veto attention message (full view)
- `src/arkive-web/src/hooks/use-fleet.ts` — Added useVetoReviews and useResolveVeto hooks
- `src/arkive-web/src/components/fleet/veto-review-card.tsx` — Created VetoReviewCard with accept/override/exclude actions
- `src/arkive-web/src/components/fleet/veto-review-panel.tsx` — Created VetoReviewPanel with loading/empty states
- `src/arkive-web/src/app/(dashboard)/fleet/[tenantId]/page.tsx` — Added VetoReviewPanel conditional render

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Full implementation complete — backend endpoints, frontend components, hooks, and types
- 2026-02-02: Code review — 3 HIGH, 4 MEDIUM, 1 LOW findings. All HIGH and MEDIUM fixed:
  - #1 [HIGH] Story file updated with completed tasks and file list
  - #2 [HIGH] Track resolving operationId to scope loading state per card
  - #3 [HIGH] Append trailing slash to library path prefix match
  - #4 [MEDIUM] Set FileMetadata.ArchiveStatus = PendingArchive on override
  - #5 [MEDIUM] Add null guard for FileMetadata in GetVetoReviews
  - #6 [MEDIUM] Add Take(200) safety cap to GetVetoReviews
  - #7 [MEDIUM] Improve ExtractLibraryPath to handle relative paths and empty library
  - #8 [LOW] Empty state in veto-review-panel — deferred (cosmetic, race condition window is narrow)
- 2026-02-02: All fixes verified — backend 0 errors, frontend build/lint clean, 120 tests pass. Story complete.
