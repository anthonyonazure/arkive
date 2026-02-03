# Story 5.3: Auto-Approval Timer for Unresponsive Requests

## Status: done

## Story
As an MSP administrator, I want unresponded approval requests to be auto-approved after a configurable period so that archiving operations are not blocked indefinitely by unresponsive site owners.

## Acceptance Criteria
- [x] ClientTenant model has configurable `AutoApprovalDays` setting (int?, default 7)
- [x] API endpoints for getting and updating tenant settings (GET/PATCH `v1/tenants/{id}/settings`)
- [x] PATCH endpoint uses proper semantics (distinguishes "not sent" from "explicitly null")
- [x] Orchestrator reads tenant's auto-approval setting before deciding on timeout behavior
- [x] When `AutoApprovalDays == 0`: immediate auto-approval, notifications skipped
- [x] When `AutoApprovalDays == null`: auto-approval disabled, max 30-day wait then skip
- [x] When `AutoApprovalDays == 1-365`: wait N days, then auto-approve on timeout
- [x] Auto-approved operations marked with `ApprovedBy = "System:AutoApproval"`
- [x] Auto-approvals tracked in `ArchiveOrchestrationResult.AutoApprovals`
- [x] Validation: AutoApprovalDays must be 0-365 or null

## Tasks
- [x] Add `AutoApprovalDays` property to ClientTenant model
- [x] Add EF configuration for `AutoApprovalDays` with default value 7
- [x] Create `TenantSettingsDto` and `UpdateTenantSettingsRequest` DTOs
- [x] Add `GetTenantSettings` and `UpdateTenantSettings` API endpoints
- [x] Add `GetTenantAutoApprovalDays` activity to NotificationActivities
- [x] Add `AutoApproveExpiredOperations` activity to NotificationActivities
- [x] Update ArchiveOrchestrator for configurable timeout with auto-approval
- [x] Add `AutoApprovals` counter to ArchiveOrchestrationResult
- [x] Add unit tests for new ClientTenant properties

## Dev Agent Record

### File List
- `src/Arkive.Core/Models/ClientTenant.cs` — Added `AutoApprovalDays` property (int?, default 7)
- `src/Arkive.Data/Configurations/ClientTenantConfig.cs` — Added EF config with default value 7
- `src/Arkive.Core/DTOs/TenantSettingsDto.cs` — Created DTOs with JsonElement-based PATCH semantics
- `src/Arkive.Core/DTOs/TeamsNotificationDto.cs` — Added `GetAutoApprovalDaysInput` and `AutoApproveExpiredInput` DTOs
- `src/Arkive.Functions/Api/TenantEndpoints.cs` — Added GET/PATCH settings endpoints with validation
- `src/Arkive.Functions/Orchestrators/NotificationActivities.cs` — Added `GetTenantAutoApprovalDays` and `AutoApproveExpiredOperations` activities
- `src/Arkive.Functions/Orchestrators/ArchiveOrchestrator.cs` — Restructured approval flow: check setting before notifications, fan-out auto-approval, configurable timeout
- `tests/Arkive.Tests/Unit/Data/ClientTenantTests.cs` — Added tests for AutoApprovalDays and ReviewFlagged defaults/setting

### Change Log
- 2026-02-02: Initial implementation of all Story 5.3 tasks
- 2026-02-02: Code review completed — 7 findings (3 HIGH, 3 MEDIUM, 1 LOW)
  - Fixed #1 HIGH: Moved auto-approval check before notifications to avoid sending useless cards when AutoApprovalDays=0
  - Fixed #2 HIGH: GetTenantAutoApprovalDays now throws for missing tenants instead of silent null
  - Fixed #3 HIGH: PATCH endpoint now uses JsonElement? to distinguish missing vs explicit null
  - Fixed #5 MEDIUM: Immediate auto-approval now uses fan-out (Task.WhenAll) instead of sequential
  - Fixed #6 MEDIUM: Added test assertions for AutoApprovalDays and ReviewFlagged properties
  - Fixed #7 LOW: Extracted magic number 30 to `MaxWaitDaysWhenDisabled` constant
  - Noted #4 MEDIUM: EF migration deferred (migrations managed separately)
- Build: 0 errors, 0 warnings (backend + frontend + lint)
- Tests: 120 passed, 0 failed
