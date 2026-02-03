using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class SavingsSnapshotService : ISavingsSnapshotService
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<SavingsSnapshotService> _logger;
    private const int StaleDaysThreshold = 180;

    public SavingsSnapshotService(ArkiveDbContext db, ILogger<SavingsSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CaptureSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var month = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        var staleCutoff = DateTimeOffset.UtcNow.AddDays(-StaleDaysThreshold);
        var now = DateTimeOffset.UtcNow;

        // Get all active orgs
        var orgIds = await _db.MspOrganizations
            .AsNoTracking()
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        foreach (var orgId in orgIds)
        {
            // Per-tenant snapshots
            var tenantData = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == orgId && t.Status == TenantStatus.Connected)
                .Select(t => new
                {
                    t.Id,
                    TotalStorageBytes = t.SharePointSites
                        .Where(s => s.IsSelected)
                        .Sum(s => s.StorageUsedBytes),
                    ArchivedBytes = t.FileMetadata
                        .Where(f => f.ArchiveStatus == "Archived")
                        .Sum(f => (long?)f.SizeBytes) ?? 0L,
                    ArchivedCoolBytes = t.FileMetadata
                        .Where(f => f.ArchiveStatus == "Archived" && (f.BlobTier == null || f.BlobTier == "Cool"))
                        .Sum(f => (long?)f.SizeBytes) ?? 0L,
                    ArchivedColdBytes = t.FileMetadata
                        .Where(f => f.ArchiveStatus == "Archived" && f.BlobTier == "Cold")
                        .Sum(f => (long?)f.SizeBytes) ?? 0L,
                    ArchivedArchiveBytes = t.FileMetadata
                        .Where(f => f.ArchiveStatus == "Archived" && f.BlobTier == "Archive")
                        .Sum(f => (long?)f.SizeBytes) ?? 0L,
                    StaleBytes = t.FileMetadata
                        .Where(f => (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff)
                        .Sum(f => (long?)f.SizeBytes) ?? 0L,
                })
                .ToListAsync(cancellationToken);

            foreach (var t in tenantData)
            {
                var coolGb = t.ArchivedCoolBytes / (decimal)(1024L * 1024 * 1024);
                var coldGb = t.ArchivedColdBytes / (decimal)(1024L * 1024 * 1024);
                var archiveGb = t.ArchivedArchiveBytes / (decimal)(1024L * 1024 * 1024);
                var staleGb = t.StaleBytes / (decimal)(1024L * 1024 * 1024);

                var savingsAchieved = coolGb * FleetAnalyticsService.SavingsForTier("Cool")
                                    + coldGb * FleetAnalyticsService.SavingsForTier("Cold")
                                    + archiveGb * FleetAnalyticsService.SavingsForTier("Archive");
                var savingsPotential = staleGb * FleetAnalyticsService.SavingsForTier("Cool") + savingsAchieved;

                await UpsertSnapshotAsync(orgId, t.Id, month, t.TotalStorageBytes, t.ArchivedBytes,
                    t.StaleBytes, Math.Round(savingsAchieved, 2), Math.Round(savingsPotential, 2), now, cancellationToken);
            }

            // Org-level aggregate snapshot (ClientTenantId = null)
            var orgTotal = tenantData.Sum(t => t.TotalStorageBytes);
            var orgArchived = tenantData.Sum(t => t.ArchivedBytes);
            var orgStale = tenantData.Sum(t => t.StaleBytes);

            var orgCoolGb = tenantData.Sum(t => t.ArchivedCoolBytes) / (decimal)(1024L * 1024 * 1024);
            var orgColdGb = tenantData.Sum(t => t.ArchivedColdBytes) / (decimal)(1024L * 1024 * 1024);
            var orgArchiveGb = tenantData.Sum(t => t.ArchivedArchiveBytes) / (decimal)(1024L * 1024 * 1024);
            var orgStaleGb = orgStale / (decimal)(1024L * 1024 * 1024);

            var orgSavingsAchieved = orgCoolGb * FleetAnalyticsService.SavingsForTier("Cool")
                                   + orgColdGb * FleetAnalyticsService.SavingsForTier("Cold")
                                   + orgArchiveGb * FleetAnalyticsService.SavingsForTier("Archive");
            var orgSavingsPotential = orgStaleGb * FleetAnalyticsService.SavingsForTier("Cool") + orgSavingsAchieved;

            await UpsertSnapshotAsync(orgId, null, month, orgTotal, orgArchived,
                orgStale, Math.Round(orgSavingsAchieved, 2), Math.Round(orgSavingsPotential, 2), now, cancellationToken);

            // Batch save all changes for this org in a single round-trip
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Captured savings snapshots for org {MspOrgId}, month {Month}: {TenantCount} tenants",
                orgId, month, tenantData.Count);
        }
    }

    public async Task<SavingsTrendResult> GetTrendsAsync(Guid mspOrgId, Guid? tenantId = null, int months = 12, CancellationToken cancellationToken = default)
    {
        var cutoffMonth = DateTimeOffset.UtcNow.AddMonths(-months).ToString("yyyy-MM");

        var query = _db.MonthlySavingsSnapshots
            .AsNoTracking()
            .Where(s => s.MspOrgId == mspOrgId && s.Month.CompareTo(cutoffMonth) >= 0);

        if (tenantId.HasValue)
            query = query.Where(s => s.ClientTenantId == tenantId.Value);
        else
            query = query.Where(s => s.ClientTenantId == null); // org-level snapshots

        var snapshots = await query
            .OrderBy(s => s.Month)
            .Select(s => new SavingsTrendDto
            {
                Month = s.Month,
                SavingsAchieved = s.SavingsAchieved,
                SavingsPotential = s.SavingsPotential,
                TotalStorageBytes = s.TotalStorageBytes,
                ArchivedStorageBytes = s.ArchivedStorageBytes,
            })
            .ToListAsync(cancellationToken);

        var currentMonth = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        var previousMonth = DateTimeOffset.UtcNow.AddMonths(-1).ToString("yyyy-MM");

        // Current is the latest snapshot matching currentMonth, or the last snapshot if current month hasn't been captured yet
        var current = snapshots.FirstOrDefault(s => s.Month == currentMonth)
                   ?? snapshots.LastOrDefault()
                   ?? new SavingsTrendDto { Month = currentMonth };

        var previous = snapshots.FirstOrDefault(s => s.Month == previousMonth);

        return new SavingsTrendResult
        {
            Months = snapshots,
            Current = current,
            Previous = previous,
        };
    }

    private async Task UpsertSnapshotAsync(
        Guid mspOrgId, Guid? clientTenantId, string month,
        long totalStorageBytes, long archivedStorageBytes, long staleStorageBytes,
        decimal savingsAchieved, decimal savingsPotential,
        DateTimeOffset capturedAt, CancellationToken cancellationToken)
    {
        var existing = await _db.MonthlySavingsSnapshots
            .FirstOrDefaultAsync(s =>
                s.MspOrgId == mspOrgId &&
                s.ClientTenantId == clientTenantId &&
                s.Month == month,
                cancellationToken);

        if (existing != null)
        {
            existing.TotalStorageBytes = totalStorageBytes;
            existing.ArchivedStorageBytes = archivedStorageBytes;
            existing.StaleStorageBytes = staleStorageBytes;
            existing.SavingsAchieved = savingsAchieved;
            existing.SavingsPotential = savingsPotential;
            existing.CapturedAt = capturedAt;
        }
        else
        {
            _db.MonthlySavingsSnapshots.Add(new MonthlySavingsSnapshot
            {
                MspOrgId = mspOrgId,
                ClientTenantId = clientTenantId,
                Month = month,
                TotalStorageBytes = totalStorageBytes,
                ArchivedStorageBytes = archivedStorageBytes,
                StaleStorageBytes = staleStorageBytes,
                SavingsAchieved = savingsAchieved,
                SavingsPotential = savingsPotential,
                CapturedAt = capturedAt,
            });
        }

    }
}
