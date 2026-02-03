using System.Text.Json;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class RuleEvaluationService : IRuleEvaluationService
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<RuleEvaluationService> _logger;

    public RuleEvaluationService(ArkiveDbContext db, ILogger<RuleEvaluationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RuleEvaluationResultDto> EvaluateFileAsync(
        Guid tenantId,
        Guid mspOrgId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var file = await _db.FileMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ClientTenantId == tenantId, cancellationToken);

        if (file is null)
            throw new InvalidOperationException("File not found.");

        var rules = await GetActiveRulesAsync(tenantId, mspOrgId, cancellationToken);

        return EvaluateFile(file, rules);
    }

    public async Task<IReadOnlyList<RuleEvaluationResultDto>> EvaluateAllFilesAsync(
        Guid tenantId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetActiveRulesAsync(tenantId, mspOrgId, cancellationToken);
        if (rules.Count == 0)
            return [];

        var totalCount = await _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId && f.ArchiveStatus == "Active")
            .CountAsync(cancellationToken);

        if (totalCount > 50_000)
            _logger.LogWarning("Tenant {TenantId} has {FileCount} active files — evaluation may be slow", tenantId, totalCount);

        var files = await _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId && f.ArchiveStatus == "Active")
            .OrderBy(f => f.Id)
            .Take(50_000) // Cap to prevent OOM for full evaluation
            .ToListAsync(cancellationToken);

        var results = new List<RuleEvaluationResultDto>();
        foreach (var file in files)
        {
            var result = EvaluateFile(file, rules);
            if (result.MatchedArchiveRuleId.HasValue || result.IsExcluded)
                results.Add(result);
        }

        _logger.LogInformation(
            "Evaluated {FileCount} files for tenant {TenantId}: {MatchCount} matched, {ExcludedCount} excluded",
            files.Count, tenantId,
            results.Count(r => r.MatchedArchiveRuleId.HasValue && !r.IsExcluded),
            results.Count(r => r.IsExcluded));

        return results;
    }

    public async Task<ExclusionScopeDto> GetExclusionScopeAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _db.ArchiveRules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == ruleId && r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId && r.RuleType == "exclusion",
                cancellationToken);

        if (rule is null)
            throw new InvalidOperationException("Exclusion rule not found.");

        var criteria = ParseCriteria(rule.Criteria);

        // Build query for matching files
        var query = _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId && f.ArchiveStatus == "Active");

        query = ApplyExclusionFilter(query, criteria);

        var scope = await query
            .GroupBy(_ => 1)
            .Select(g => new ExclusionScopeDto
            {
                RuleId = ruleId,
                AffectedFileCount = g.Count(),
                AffectedSizeBytes = g.Sum(f => (long?)f.SizeBytes) ?? 0L,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return scope ?? new ExclusionScopeDto { RuleId = ruleId };
    }

    // Cost constants for savings estimation (per GB per month)
    private const decimal SharePointCostPerGbMonth = 0.20m;
    private static readonly Dictionary<string, decimal> TierCostPerGbMonth = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Cool", 0.01m },
        { "Cold", 0.006m },
        { "Archive", 0.002m },
    };

    public async Task<DryRunPreviewDto> PreviewRuleAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _db.ArchiveRules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == ruleId && r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId,
                cancellationToken);

        if (rule is null)
            throw new InvalidOperationException("Rule not found.");

        return await ExecutePreviewAsync(tenantId, mspOrgId, rule.RuleType, rule.Criteria, rule.TargetTier, cancellationToken);
    }

    private static readonly HashSet<string> PreviewableRuleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "age", "size", "type", "owner"
    };

    private static readonly HashSet<string> ValidPreviewTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cool", "Cold", "Archive"
    };

    public async Task<DryRunPreviewDto> PreviewAdHocRuleAsync(
        Guid tenantId,
        Guid mspOrgId,
        DryRunPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.RuleType);
        ArgumentException.ThrowIfNullOrEmpty(request.Criteria);

        if (!PreviewableRuleTypes.Contains(request.RuleType))
            throw new ArgumentException($"Invalid rule type '{request.RuleType}' for preview. Exclusion rules cannot be previewed as archive candidates.");

        if (!ValidPreviewTiers.Contains(request.TargetTier))
            throw new ArgumentException($"Invalid target tier '{request.TargetTier}'.");

        try
        {
            using var doc = JsonDocument.Parse(request.Criteria);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Criteria must be a JSON object.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("Criteria must be valid JSON.");
        }

        return await ExecutePreviewAsync(tenantId, mspOrgId, request.RuleType, request.Criteria, request.TargetTier, cancellationToken);
    }

    private async Task<DryRunPreviewDto> ExecutePreviewAsync(
        Guid tenantId,
        Guid mspOrgId,
        string ruleType,
        string criteriaJson,
        string targetTier,
        CancellationToken cancellationToken)
    {
        // Load files (capped at 50K)
        var files = await _db.FileMetadata
            .AsNoTracking()
            .Where(f => f.ClientTenantId == tenantId && f.ArchiveStatus == "Active")
            .OrderBy(f => f.Id)
            .Take(50_000)
            .ToListAsync(cancellationToken);

        // Load active exclusion rules for this tenant
        var exclusionRules = await _db.ArchiveRules
            .AsNoTracking()
            .Where(r => r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId && r.IsActive && r.RuleType == "exclusion")
            .ToListAsync(cancellationToken);

        // Build a synthetic archive rule for matching
        var syntheticRule = new ArchiveRule
        {
            RuleType = ruleType,
            Criteria = criteriaJson,
            TargetTier = targetTier,
        };

        var matchedFiles = new List<FileMetadata>();
        var excludedCount = 0;

        foreach (var file in files)
        {
            // Check if any exclusion rule matches first
            var isExcluded = false;
            foreach (var exclusion in exclusionRules)
            {
                if (FileMatchesExclusionRule(file, exclusion))
                {
                    isExcluded = true;
                    break;
                }
            }

            if (isExcluded)
            {
                excludedCount++;
                continue;
            }

            // Check if the rule matches this file
            if (FileMatchesArchiveRule(file, syntheticRule))
                matchedFiles.Add(file);
        }

        // Aggregate top sites (up to 5)
        var topSites = matchedFiles
            .GroupBy(f => f.SiteId)
            .Select(g => new SiteImpactDto
            {
                SiteId = g.Key,
                DisplayName = g.Key, // SiteId used as display name; will be resolved by UI
                FileCount = g.Count(),
                SizeBytes = g.Sum(f => f.SizeBytes),
            })
            .OrderByDescending(s => s.SizeBytes)
            .Take(5)
            .ToList();

        // Calculate estimated annual savings
        var totalSizeBytes = matchedFiles.Sum(f => f.SizeBytes);
        var totalSizeGb = totalSizeBytes / (1024m * 1024m * 1024m);
        var tierCost = TierCostPerGbMonth.GetValueOrDefault(targetTier, 0.01m);
        var monthlySavings = totalSizeGb * (SharePointCostPerGbMonth - tierCost);
        var annualSavings = Math.Max(0, monthlySavings * 12);

        _logger.LogInformation(
            "Preview for tenant {TenantId}: {MatchCount} files ({SizeBytes} bytes), {ExcludedCount} excluded, ${Savings:F2}/year",
            tenantId, matchedFiles.Count, totalSizeBytes, excludedCount, annualSavings);

        return new DryRunPreviewDto
        {
            FileCount = matchedFiles.Count,
            TotalSizeBytes = totalSizeBytes,
            EstimatedAnnualSavings = Math.Round(annualSavings, 2),
            TopSites = topSites,
            ExcludedFileCount = excludedCount,
        };
    }

    private async Task<List<ArchiveRule>> GetActiveRulesAsync(
        Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken)
    {
        return await _db.ArchiveRules
            .AsNoTracking()
            .Where(r => r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId && r.IsActive)
            .ToListAsync(cancellationToken);
    }

    private static RuleEvaluationResultDto EvaluateFile(FileMetadata file, List<ArchiveRule> rules)
    {
        var result = new RuleEvaluationResultDto
        {
            FileId = file.Id,
            FileName = file.FileName,
        };

        // Check exclusion rules FIRST — exclusions always win
        var exclusionRules = rules.Where(r => r.RuleType == "exclusion");
        foreach (var exclusion in exclusionRules)
        {
            if (FileMatchesExclusionRule(file, exclusion))
            {
                result.IsExcluded = true;
                result.MatchedExclusionRuleId = exclusion.Id;
                return result;
            }
        }

        // Then check archive rules
        var archiveRules = rules.Where(r => r.RuleType != "exclusion");
        foreach (var archiveRule in archiveRules)
        {
            if (FileMatchesArchiveRule(file, archiveRule))
            {
                result.MatchedArchiveRuleId = archiveRule.Id;
                result.TargetTier = archiveRule.TargetTier;
                return result;
            }
        }

        return result;
    }

    private static bool FileMatchesExclusionRule(FileMetadata file, ArchiveRule rule)
    {
        var criteria = ParseCriteria(rule.Criteria);

        // Any matching criterion excludes the file (OR logic)
        if (criteria.TryGetProperty("libraryPath", out var libPath))
        {
            if (file.FilePath.StartsWith(libPath.GetString() ?? "", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (criteria.TryGetProperty("folderPath", out var folderPath))
        {
            if (file.FilePath.StartsWith(folderPath.GetString() ?? "", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (criteria.TryGetProperty("fileTypes", out var fileTypes) && fileTypes.ValueKind == JsonValueKind.Array)
        {
            foreach (var ft in fileTypes.EnumerateArray())
            {
                if (string.Equals(file.FileType, ft.GetString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        // complianceTags: deferred — no compliance metadata in FileMetadata yet

        return false;
    }

    private static bool FileMatchesArchiveRule(FileMetadata file, ArchiveRule rule)
    {
        var criteria = ParseCriteria(rule.Criteria);

        return rule.RuleType switch
        {
            "age" => MatchesAgeRule(file, criteria),
            "size" => MatchesSizeRule(file, criteria),
            "type" => MatchesTypeRule(file, criteria),
            "owner" => MatchesOwnerRule(file, criteria),
            _ => false,
        };
    }

    private static bool MatchesAgeRule(FileMetadata file, JsonElement criteria)
    {
        if (!criteria.TryGetProperty("inactiveDays", out var daysEl))
            return false;

        var inactiveDays = daysEl.GetInt32();
        var lastActivity = file.LastAccessedAt ?? file.LastModifiedAt;
        return (DateTimeOffset.UtcNow - lastActivity).TotalDays >= inactiveDays;
    }

    private static bool MatchesSizeRule(FileMetadata file, JsonElement criteria)
    {
        if (criteria.TryGetProperty("minSizeBytes", out var minEl))
        {
            if (file.SizeBytes < minEl.GetInt64())
                return false;
        }

        if (criteria.TryGetProperty("maxSizeBytes", out var maxEl))
        {
            if (file.SizeBytes > maxEl.GetInt64())
                return false;
        }

        return criteria.TryGetProperty("minSizeBytes", out _) || criteria.TryGetProperty("maxSizeBytes", out _);
    }

    private static bool MatchesTypeRule(FileMetadata file, JsonElement criteria)
    {
        if (!criteria.TryGetProperty("fileTypes", out var typesEl) || typesEl.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var ft in typesEl.EnumerateArray())
        {
            if (string.Equals(file.FileType, ft.GetString(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool MatchesOwnerRule(FileMetadata file, JsonElement criteria)
    {
        if (!criteria.TryGetProperty("owners", out var ownersEl) || ownersEl.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var owner in ownersEl.EnumerateArray())
        {
            if (string.Equals(file.Owner, owner.GetString(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static JsonElement ParseCriteria(string criteriaJson)
    {
        using var doc = JsonDocument.Parse(criteriaJson);
        return doc.RootElement.Clone();
    }

    private static IQueryable<FileMetadata> ApplyExclusionFilter(
        IQueryable<FileMetadata> query, JsonElement criteria)
    {
        // Build OR predicates — any matching criterion excludes the file
        var predicates = new List<System.Linq.Expressions.Expression<Func<FileMetadata, bool>>>();

        if (criteria.TryGetProperty("libraryPath", out var libPath))
        {
            var path = libPath.GetString() ?? "";
            predicates.Add(f => f.FilePath.StartsWith(path));
        }

        if (criteria.TryGetProperty("folderPath", out var folderPath))
        {
            var path = folderPath.GetString() ?? "";
            predicates.Add(f => f.FilePath.StartsWith(path));
        }

        if (criteria.TryGetProperty("fileTypes", out var fileTypes) && fileTypes.ValueKind == JsonValueKind.Array)
        {
            var types = fileTypes.EnumerateArray().Select(ft => ft.GetString()!).ToList();
            predicates.Add(f => types.Contains(f.FileType));
        }

        if (predicates.Count == 0)
            return query.Where(f => false);

        // Combine with OR
        var param = System.Linq.Expressions.Expression.Parameter(typeof(FileMetadata), "f");
        System.Linq.Expressions.Expression? combined = null;
        foreach (var predicate in predicates)
        {
            var body = System.Linq.Expressions.Expression.Invoke(predicate, param);
            combined = combined is null ? body : System.Linq.Expressions.Expression.OrElse(combined, body);
        }

        var lambda = System.Linq.Expressions.Expression.Lambda<Func<FileMetadata, bool>>(combined!, param);
        return query.Where(lambda);
    }
}
