using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class FleetAnalyticsService : IFleetAnalyticsService
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<FleetAnalyticsService> _logger;

    // SharePoint Online: ~$200/TB/mo = ~$0.20/GB/mo
    private const decimal SharePointCostPerGbMonth = 0.20m;
    // Azure Blob tier costs per GB/mo
    private const decimal CoolTierCostPerGbMonth = 0.01m;    // ~$10/TB/mo
    private const decimal ColdTierCostPerGbMonth = 0.003m;   // ~$3/TB/mo
    private const decimal ArchiveTierCostPerGbMonth = 0.001m; // ~$1/TB/mo
    private const int StaleDaysThreshold = 180;

    /// <summary>Returns the Azure Blob cost per GB/mo for the given tier.</summary>
    internal static decimal BlobCostForTier(string? tier) => tier switch
    {
        "Cold" => ColdTierCostPerGbMonth,
        "Archive" => ArchiveTierCostPerGbMonth,
        _ => CoolTierCostPerGbMonth, // default / Cool
    };

    /// <summary>Returns savings per GB/mo for the given tier compared to SharePoint.</summary>
    internal static decimal SavingsForTier(string? tier) =>
        SharePointCostPerGbMonth - BlobCostForTier(tier);

    public FleetAnalyticsService(ArkiveDbContext db, ILogger<FleetAnalyticsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FleetOverviewDto> GetFleetOverviewAsync(Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var staleCutoff = DateTimeOffset.UtcNow.AddDays(-StaleDaysThreshold);

        // Query per-tenant analytics in a single DB round-trip
        var tenantAnalytics = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.MspOrgId == mspOrgId)
            .Select(t => new
            {
                t.Id,
                t.DisplayName,
                t.Status,
                t.ConnectedAt,
                t.LastScannedAt,
                t.CreatedAt,
                SelectedSiteCount = t.SharePointSites.Count(s => s.IsSelected),
                TotalStorageBytes = t.SharePointSites
                    .Where(s => s.IsSelected)
                    .Sum(s => s.StorageUsedBytes),
                // Stale = files whose effective last access is older than threshold
                StaleStorageBytes = t.FileMetadata
                    .Where(f => (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff)
                    .Sum(f => (long?)f.SizeBytes) ?? 0L,
                TotalFileStorageBytes = t.FileMetadata
                    .Sum(f => (long?)f.SizeBytes) ?? 0L,
                // Files already archived (moved to Blob) — per tier
                ArchivedStorageBytes = t.FileMetadata
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
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        // Get veto counts per tenant (operations with Status == "Vetoed")
        var tenantIds = tenantAnalytics.Select(t => t.Id).ToList();
        var vetoCounts = await _db.ArchiveOperations
            .AsNoTracking()
            .Where(o => tenantIds.Contains(o.ClientTenantId) && o.Status == "Vetoed")
            .GroupBy(o => o.ClientTenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var vetoCountMap = vetoCounts.ToDictionary(v => v.TenantId, v => v.Count);

        var fleetTenants = tenantAnalytics.Select(t =>
        {
            var vetoCount = vetoCountMap.GetValueOrDefault(t.Id, 0);
            var totalFileBytes = t.TotalFileStorageBytes > 0 ? t.TotalFileStorageBytes : t.TotalStorageBytes;
            var staleGb = t.StaleStorageBytes / (decimal)(1024L * 1024 * 1024);
            var stalePercentage = totalFileBytes > 0
                ? (double)t.StaleStorageBytes / totalFileBytes * 100.0
                : 0.0;

            // Tier-aware savings achieved = sum of per-tier (SPO cost - Blob cost) * archived GB
            var coolGb = t.ArchivedCoolBytes / (decimal)(1024L * 1024 * 1024);
            var coldGb = t.ArchivedColdBytes / (decimal)(1024L * 1024 * 1024);
            var archiveGb = t.ArchivedArchiveBytes / (decimal)(1024L * 1024 * 1024);
            var savingsAchieved = coolGb * SavingsForTier("Cool")
                               + coldGb * SavingsForTier("Cold")
                               + archiveGb * SavingsForTier("Archive");

            // Potential = stale data at Cool tier rate + already-achieved savings
            var savingsPotential = staleGb * SavingsForTier("Cool") + savingsAchieved;

            return new FleetTenantDto
            {
                Id = t.Id,
                DisplayName = t.DisplayName,
                Status = t.Status,
                ConnectedAt = t.ConnectedAt,
                SelectedSiteCount = t.SelectedSiteCount,
                TotalStorageBytes = t.TotalStorageBytes,
                StaleStorageBytes = t.StaleStorageBytes,
                SavingsAchieved = Math.Round(savingsAchieved, 2),
                SavingsPotential = Math.Round(savingsPotential, 2),
                StalePercentage = Math.Round(stalePercentage, 1),
                LastScanTime = t.LastScannedAt,
                AttentionType = ClassifyAttention(t.Status, t.LastScannedAt, vetoCount),
                VetoCount = vetoCount,
                CreatedAt = t.CreatedAt,
            };
        }).ToList();

        // Sort: attention tenants first (by severity), then all-clear by name
        fleetTenants.Sort((a, b) =>
        {
            var orderA = AttentionSortOrder(a.AttentionType);
            var orderB = AttentionSortOrder(b.AttentionType);
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        var scannedTenants = fleetTenants.Where(t => t.Status == TenantStatus.Connected).ToList();

        var hero = new FleetHeroSavingsDto
        {
            SavingsAchieved = scannedTenants.Sum(t => t.SavingsAchieved),
            SavingsPotential = scannedTenants.Sum(t => t.SavingsPotential),
            SavingsUncaptured = scannedTenants.Sum(t => t.SavingsPotential - t.SavingsAchieved),
            TenantCount = scannedTenants.Count,
        };

        _logger.LogInformation(
            "Fleet overview for org {MspOrgId}: {TenantCount} tenants, ${SavingsAchieved}/mo saved, ${SavingsPotential}/mo potential",
            mspOrgId, hero.TenantCount, hero.SavingsAchieved, hero.SavingsPotential);

        return new FleetOverviewDto
        {
            HeroSavings = hero,
            Tenants = fleetTenants,
        };
    }

    public async Task<TenantAnalyticsDto> GetTenantAnalyticsAsync(Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var staleCutoff = DateTimeOffset.UtcNow.AddDays(-StaleDaysThreshold);

        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && t.MspOrgId == mspOrgId)
            .Select(t => new
            {
                t.Id,
                t.DisplayName,
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
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        // Per-site breakdown: two separate queries to avoid correlated subquery issues
        var selectedSites = await _db.SharePointSites
            .AsNoTracking()
            .Where(s => s.ClientTenantId == tenantId && s.IsSelected)
            .Select(s => new { s.SiteId, s.DisplayName, s.StorageUsedBytes })
            .OrderByDescending(s => s.StorageUsedBytes)
            .ToListAsync(cancellationToken);

        var staleBytesPerSite = await _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId)
            .Where(f => (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff)
            .GroupBy(f => f.SiteId)
            .Select(g => new { SiteId = g.Key, StaleBytes = g.Sum(f => (long?)f.SizeBytes) ?? 0L })
            .ToListAsync(cancellationToken);

        var staleLookup = staleBytesPerSite.ToDictionary(x => x.SiteId, x => x.StaleBytes);

        var sites = selectedSites.Select(s =>
        {
            var staleBytes = staleLookup.GetValueOrDefault(s.SiteId, 0L);
            var stalePercentage = s.StorageUsedBytes > 0
                ? (double)staleBytes / s.StorageUsedBytes * 100.0
                : 0.0;
            var staleGb = staleBytes / (decimal)(1024L * 1024 * 1024);

            return new SiteBreakdownDto
            {
                SiteId = s.SiteId,
                DisplayName = s.DisplayName,
                TotalStorageBytes = s.StorageUsedBytes,
                ActiveStorageBytes = s.StorageUsedBytes - staleBytes,
                StaleStorageBytes = staleBytes,
                StalePercentage = Math.Round(stalePercentage, 1),
                PotentialSavings = Math.Round(staleGb * SavingsForTier("Cool"), 2),
            };
        }).ToList();

        var totalGb = tenant.TotalStorageBytes / (decimal)(1024L * 1024 * 1024);
        var staleGbTotal = tenant.StaleBytes / (decimal)(1024L * 1024 * 1024);

        // Tier-aware savings achieved
        var coolGb = tenant.ArchivedCoolBytes / (decimal)(1024L * 1024 * 1024);
        var coldGb = tenant.ArchivedColdBytes / (decimal)(1024L * 1024 * 1024);
        var archiveGb = tenant.ArchivedArchiveBytes / (decimal)(1024L * 1024 * 1024);
        var savingsAchieved = coolGb * SavingsForTier("Cool")
                            + coldGb * SavingsForTier("Cold")
                            + archiveGb * SavingsForTier("Archive");
        var savingsPotential = staleGbTotal * SavingsForTier("Cool") + savingsAchieved;
        var currentSpend = totalGb * SharePointCostPerGbMonth;
        var archiveSavings = staleGbTotal * SavingsForTier("Cool");
        var netCostOptimized = currentSpend - archiveSavings;

        return new TenantAnalyticsDto
        {
            TenantId = tenant.Id,
            DisplayName = tenant.DisplayName,
            TotalStorageBytes = tenant.TotalStorageBytes,
            SavingsAchieved = Math.Round(savingsAchieved, 2),
            SavingsPotential = Math.Round(savingsPotential, 2),
            CostAnalysis = new CostAnalysisDto
            {
                CurrentSpendPerMonth = Math.Round(currentSpend, 2),
                PotentialArchiveSavings = Math.Round(archiveSavings, 2),
                NetCostIfOptimized = Math.Round(netCostOptimized, 2),
            },
            Sites = sites,
        };
    }

    private static string ClassifyAttention(TenantStatus status, DateTimeOffset? lastScannedAt, int vetoCount = 0)
    {
        if (status == TenantStatus.Error)
            return "error";

        if (status != TenantStatus.Connected)
            return "all-clear";

        // Vetoed operations needing review take priority over new-scan
        if (vetoCount > 0)
            return "veto-review";

        // New scan results: scanned within last 24 hours
        if (lastScannedAt.HasValue &&
            (DateTimeOffset.UtcNow - lastScannedAt.Value).TotalHours < 24)
            return "new-scan";

        return "all-clear";
    }

    private static int AttentionSortOrder(string attentionType) => attentionType switch
    {
        "error" => 0,
        "veto-review" => 1,
        "new-scan" => 2,
        "all-clear" => 3,
        _ => 4,
    };

    public async Task<SiteFilesDto> GetSiteFilesAsync(
        Guid tenantId, string siteId, Guid mspOrgId,
        int page = 1, int pageSize = 50,
        string? sortBy = null, string? sortDir = null,
        int? minAgeDays = null, string? fileType = null,
        long? minSizeBytes = null, long? maxSizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        var staleCutoff = DateTimeOffset.UtcNow.AddDays(-StaleDaysThreshold);

        // Verify site belongs to tenant and org
        var site = await _db.SharePointSites
            .AsNoTracking()
            .Where(s => s.ClientTenantId == tenantId && s.SiteId == siteId && s.MspOrgId == mspOrgId)
            .Select(s => new { s.SiteId, s.DisplayName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Site {siteId} not found.");

        // Build base query
        var query = _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId && f.SiteId == siteId);

        // Apply filters
        if (minAgeDays.HasValue)
        {
            var ageCutoff = DateTimeOffset.UtcNow.AddDays(-minAgeDays.Value);
            query = query.Where(f => (f.LastAccessedAt ?? f.LastModifiedAt) < ageCutoff);
        }

        if (!string.IsNullOrEmpty(fileType))
        {
            query = query.Where(f => f.FileType == fileType);
        }

        if (minSizeBytes.HasValue)
        {
            query = query.Where(f => f.SizeBytes >= minSizeBytes.Value);
        }

        if (maxSizeBytes.HasValue)
        {
            query = query.Where(f => f.SizeBytes <= maxSizeBytes.Value);
        }

        // Get summary stats for filtered query
        var summaryData = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalFileCount = g.Count(),
                TotalSizeBytes = g.Sum(f => (long?)f.SizeBytes) ?? 0L,
                StaleFileCount = g.Count(f => (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff),
                StaleSizeBytes = g.Where(f => (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff)
                    .Sum(f => (long?)f.SizeBytes) ?? 0L,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalCount = summaryData?.TotalFileCount ?? 0;

        // Apply sorting
        var sortAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        query = sortBy?.ToLowerInvariant() switch
        {
            "name" => sortAsc ? query.OrderBy(f => f.FileName) : query.OrderByDescending(f => f.FileName),
            "size" => sortAsc ? query.OrderBy(f => f.SizeBytes) : query.OrderByDescending(f => f.SizeBytes),
            "type" => sortAsc ? query.OrderBy(f => f.FileType) : query.OrderByDescending(f => f.FileType),
            "owner" => sortAsc ? query.OrderBy(f => f.Owner) : query.OrderByDescending(f => f.Owner),
            "lastaccessed" => sortAsc
                ? query.OrderBy(f => f.LastAccessedAt ?? f.LastModifiedAt)
                : query.OrderByDescending(f => f.LastAccessedAt ?? f.LastModifiedAt),
            "status" => sortAsc ? query.OrderBy(f => f.ArchiveStatus) : query.OrderByDescending(f => f.ArchiveStatus),
            _ => query.OrderByDescending(f => f.SizeBytes), // default: largest first
        };

        // Apply pagination
        var files = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FileDetailDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FilePath = f.FilePath,
                FileType = f.FileType,
                SizeBytes = f.SizeBytes,
                Owner = f.Owner,
                LastModifiedAt = f.LastModifiedAt,
                LastAccessedAt = f.LastAccessedAt,
                ArchiveStatus = f.ArchiveStatus,
                IsStale = (f.LastAccessedAt ?? f.LastModifiedAt) < staleCutoff,
            })
            .ToListAsync(cancellationToken);

        return new SiteFilesDto
        {
            SiteId = site.SiteId,
            DisplayName = site.DisplayName,
            Summary = new FileSummaryDto
            {
                TotalFileCount = summaryData?.TotalFileCount ?? 0,
                TotalSizeBytes = summaryData?.TotalSizeBytes ?? 0L,
                StaleFileCount = summaryData?.StaleFileCount ?? 0,
                StaleSizeBytes = summaryData?.StaleSizeBytes ?? 0L,
                StaleDaysThreshold = StaleDaysThreshold,
            },
            Files = files,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };
    }
}
