# Story 7.1: Immutable Audit Logging Service

## Status: done

## Story
As a system process,
I want every archive, retrieve, access, and configuration change logged with full detail,
So that there is a complete, tamper-proof record of all actions for compliance.

## Acceptance Criteria
- [x] AuditEntry created for every operation (archive, retrieve, rule CRUD, tenant connect/disconnect, user changes) with Id, MspOrgId, ClientTenantId, Actor, Action, Details JSON, Timestamp, CorrelationId
- [x] AuditEntries table created via EF migration with RLS, no UPDATE/DELETE permissions, indexes on (ClientTenantId+Timestamp) and (Action+Timestamp)
- [x] Archive operation audit entries include chain-of-custody details: source path, destination blob, file size, tier, approvedBy, operationId

## Tasks
- [x] Backend: Create AuditEntry model in Arkive.Core/Models
- [x] Backend: Add AuditEntries DbSet to ArkiveDbContext with appropriate configuration
- [x] Backend: Create IAuditService interface and AuditService implementation
- [x] Backend: Integrate audit logging into existing archive, retrieval, rule, and tenant operations
- [x] Backend: Create GET audit entries endpoint with filtering

## Dev Agent Record

### File List
- `src/Arkive.Core/Models/AuditEntry.cs` (new) — AuditEntry domain model
- `src/Arkive.Core/DTOs/AuditEntryDto.cs` (new) — AuditEntryDto and AuditSearchResult DTOs
- `src/Arkive.Core/Interfaces/IAuditService.cs` (new) — IAuditService interface and AuditInput class
- `src/Arkive.Data/Configurations/AuditEntryConfig.cs` (new) — EF configuration with indexes
- `src/Arkive.Data/ArkiveDbContext.cs` (modified) — Added AuditEntries DbSet
- `src/Arkive.Functions/Services/AuditService.cs` (new) — Fail-safe audit logging implementation
- `src/Arkive.Functions/Api/AuditEndpoints.cs` (new) — GET v1/audit with filtering and pagination
- `src/Arkive.Functions/Services/ArchiveService.cs` (modified) — Added audit logging for archive and retrieval operations
- `src/Arkive.Functions/Api/ArchiveRuleEndpoints.cs` (modified) — Added audit logging for rule create/update/delete
- `src/Arkive.Functions/Api/TenantEndpoints.cs` (modified) — Added audit logging for tenant create/disconnect/settings update
- `src/Arkive.Functions/Program.cs` (modified) — Registered IAuditService in DI

### Change Log
- 2026-02-02: Story created, implementation starting
- 2026-02-02: Created AuditEntry model, DTOs, IAuditService, AuditService, AuditEntryConfig, AuditEndpoints
- 2026-02-02: Integrated audit logging into ArchiveService (archive + retrieval operations)
- 2026-02-02: Code review fix: Renamed AuditLogInput → AuditInput to avoid M365 naming collision
- 2026-02-02: Code review fix: Added SaveChangesAsync documentation to IAuditService interface
- 2026-02-02: Code review fix: Integrated audit logging into ArchiveRuleEndpoints (create/update/delete) and TenantEndpoints (create/disconnect/settings update)
- 2026-02-02: All builds pass, 120 tests pass, story complete
