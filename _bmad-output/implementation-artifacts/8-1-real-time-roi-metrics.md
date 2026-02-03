# Story 8.1: Real-Time ROI Metrics & Cost Calculation Engine

## Status: done

## Story
As a system process,
I want to calculate real-time cost savings based on actual storage archived and Azure tier pricing,
So that savings metrics are always accurate and up-to-date.

## Acceptance Criteria
- [x] CostCalculatorService computes: SharePoint cost avoided = archived_TB * $200/TB/month, Azure Blob cost = archived_TB * tier_price/TB/month (Cool: ~$10, Cold: ~$3, Archive: ~$1), net savings = SPO cost - Blob cost
- [x] Savings amounts reflect real-time calculations from actual archive operations on all dashboards
- [x] HeroSavings on fleet view aggregates savings across all tenants
- [x] StatCardGrid on tenant detail shows per-tenant savings
- [x] System stores monthly savings snapshots for trend reporting

## Tasks
- [x] Backend: Create MonthlySavingsSnapshot model and EF configuration
- [x] Backend: Add DbSet and register in ArkiveDbContext
- [x] Backend: Update FleetAnalyticsService to use tier-aware pricing
- [x] Backend: Create SnapshotService for capturing monthly snapshots
- [x] Backend: Create timer function to auto-capture snapshots monthly
- [x] Backend: Add GET /v1/savings/trends endpoint for monthly trend data
- [x] Frontend: Add SavingsTrend types and useSavingsTrends hook
- [x] Frontend: Update StatCardGrid with month-over-month comparison
- [x] Code review fix: Batch SaveChangesAsync per org instead of per tenant
- [x] Code review fix: Replace string.Compare with CompareTo for EF Core SQL translation
- [x] Code review fix: Neutral coloring for Total Storage delta (not inherently good/bad)

## Dev Agent Record

### File List
- `src/Arkive.Core/Models/MonthlySavingsSnapshot.cs` (new) — Monthly savings snapshot entity with org/tenant/month tracking
- `src/Arkive.Data/Configurations/MonthlySavingsSnapshotConfig.cs` (new) — EF Core configuration with unique index, FKs, decimal precision
- `src/Arkive.Data/ArkiveDbContext.cs` (modified) — Added MonthlySavingsSnapshots DbSet
- `src/Arkive.Core/DTOs/SavingsTrendDto.cs` (new) — SavingsTrendDto and SavingsTrendResult DTOs
- `src/Arkive.Core/Interfaces/ISavingsSnapshotService.cs` (new) — Interface for snapshot capture and trend queries
- `src/Arkive.Functions/Services/FleetAnalyticsService.cs` (modified) — Tier-aware pricing ($0.20/GB SPO, Cool/Cold/Archive blob tiers), per-tier archived bytes queries
- `src/Arkive.Functions/Services/SavingsSnapshotService.cs` (new) — Snapshot capture with batched upserts, trend query service
- `src/Arkive.Functions/Triggers/MonthlySavingsSnapshotTrigger.cs` (new) — Daily timer (3 AM UTC) for snapshot capture
- `src/Arkive.Functions/Api/SavingsEndpoints.cs` (new) — GET /v1/savings/trends endpoint
- `src/Arkive.Functions/Program.cs` (modified) — Registered ISavingsSnapshotService DI
- `src/arkive-web/src/types/tenant.ts` (modified) — Added SavingsTrend and SavingsTrendResult types
- `src/arkive-web/src/hooks/use-fleet.ts` (modified) — Added useSavingsTrends hook
- `src/arkive-web/src/components/fleet/stat-card-grid.tsx` (modified) — DeltaLabel with neutral mode, month-over-month comparison
- `src/arkive-web/src/app/(dashboard)/fleet/[tenantId]/page.tsx` (modified) — Wired useSavingsTrends to StatCardGrid

### Change Log
- 2026-02-02: Story created, implementation complete
- 2026-02-02: Tier-aware pricing engine (SPO $0.20/GB, Cool $0.01, Cold $0.003, Archive $0.001)
- 2026-02-02: Monthly snapshot model, timer trigger, trends API, frontend delta display
- 2026-02-02: Code review fixes — batched saves, EF-safe string comparison, neutral storage delta coloring
- 2026-02-02: All builds pass, ESLint clean, 120 tests pass
