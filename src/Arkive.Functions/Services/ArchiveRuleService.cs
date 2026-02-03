using System.Text.Json;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class ArchiveRuleService : IArchiveRuleService
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<ArchiveRuleService> _logger;

    private static readonly HashSet<string> ValidRuleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "age", "size", "type", "owner", "exclusion"
    };

    private static readonly HashSet<string> ValidTargetTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cool", "Cold", "Archive"
    };

    public ArchiveRuleService(ArkiveDbContext db, ILogger<ArchiveRuleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ArchiveRuleDto> CreateAsync(
        Guid tenantId,
        Guid mspOrgId,
        CreateArchiveRuleRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Name);
        ArgumentException.ThrowIfNullOrEmpty(request.RuleType);

        if (!ValidRuleTypes.Contains(request.RuleType))
            throw new ArgumentException($"Invalid rule type '{request.RuleType}'. Valid types: {string.Join(", ", ValidRuleTypes)}");

        if (!ValidTargetTiers.Contains(request.TargetTier))
            throw new ArgumentException($"Invalid target tier '{request.TargetTier}'. Valid tiers: {string.Join(", ", ValidTargetTiers)}");

        ValidateJsonCriteria(request.Criteria);

        if (string.Equals(request.RuleType, "exclusion", StringComparison.OrdinalIgnoreCase))
            ValidateExclusionCriteria(request.Criteria);

        // Verify tenant belongs to this org
        var tenantExists = await _db.ClientTenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.MspOrgId == mspOrgId, cancellationToken);

        if (!tenantExists)
            throw new InvalidOperationException("Tenant not found.");

        var entity = new ArchiveRule
        {
            ClientTenantId = tenantId,
            MspOrgId = mspOrgId,
            Name = request.Name,
            RuleType = request.RuleType.ToLowerInvariant(),
            Criteria = request.Criteria,
            TargetTier = request.TargetTier,
            IsActive = request.IsActive,
            CreatedBy = createdBy,
        };

        _db.ArchiveRules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created archive rule {RuleId} (type={RuleType}) for tenant {TenantId} in org {MspOrgId}",
            entity.Id, entity.RuleType, tenantId, mspOrgId);

        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<ArchiveRuleDto>> GetAllByTenantAsync(
        Guid tenantId,
        Guid mspOrgId,
        string? ruleType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ArchiveRules
            .AsNoTracking()
            .Where(r => r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId);

        if (!string.IsNullOrEmpty(ruleType))
            query = query.Where(r => r.RuleType == ruleType.ToLowerInvariant());

        var rules = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ArchiveRuleDto
            {
                Id = r.Id,
                ClientTenantId = r.ClientTenantId,
                Name = r.Name,
                RuleType = r.RuleType,
                Criteria = r.Criteria,
                TargetTier = r.TargetTier,
                IsActive = r.IsActive,
                CreatedBy = r.CreatedBy,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        // Populate scope for exclusion rules
        var exclusionRules = rules.Where(r => r.RuleType == "exclusion" && r.IsActive).ToList();
        if (exclusionRules.Count > 0)
        {
            foreach (var rule in exclusionRules)
            {
                try
                {
                    var criteria = ParseExclusionCriteria(rule.Criteria);
                    var fileQuery = _db.FileMetadata
                        .AsNoTracking()
                        .Where(f => f.ClientTenantId == tenantId && f.ArchiveStatus == "Active");

                    fileQuery = ApplyExclusionFilter(fileQuery, criteria);

                    var scope = await fileQuery
                        .GroupBy(_ => 1)
                        .Select(g => new { Count = g.Count(), Size = g.Sum(f => (long?)f.SizeBytes) ?? 0L })
                        .FirstOrDefaultAsync(cancellationToken);

                    rule.AffectedFileCount = scope?.Count ?? 0;
                    rule.AffectedSizeBytes = scope?.Size ?? 0L;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to calculate scope for exclusion rule {RuleId}", rule.Id);
                }
            }
        }

        _logger.LogDebug(
            "Retrieved {RuleCount} archive rules for tenant {TenantId}",
            rules.Count, tenantId);

        return rules;
    }

    public async Task<ArchiveRuleDto?> GetByIdAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default)
    {
        var dto = await _db.ArchiveRules
            .AsNoTracking()
            .Where(r => r.Id == ruleId && r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId)
            .Select(r => new ArchiveRuleDto
            {
                Id = r.Id,
                ClientTenantId = r.ClientTenantId,
                Name = r.Name,
                RuleType = r.RuleType,
                Criteria = r.Criteria,
                TargetTier = r.TargetTier,
                IsActive = r.IsActive,
                CreatedBy = r.CreatedBy,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            _logger.LogDebug("Archive rule {RuleId} not found for tenant {TenantId}", ruleId, tenantId);

        return dto;
    }

    public async Task<ArchiveRuleDto?> UpdateAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        UpdateArchiveRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Rule name cannot be empty.");

        if (request.RuleType is not null && !ValidRuleTypes.Contains(request.RuleType))
            throw new ArgumentException($"Invalid rule type '{request.RuleType}'.");

        if (request.TargetTier is not null && !ValidTargetTiers.Contains(request.TargetTier))
            throw new ArgumentException($"Invalid target tier '{request.TargetTier}'.");

        if (request.Criteria is not null)
        {
            ValidateJsonCriteria(request.Criteria);

            // Determine effective rule type for criteria validation
            var effectiveRuleType = request.RuleType ?? null;
            if (string.Equals(effectiveRuleType, "exclusion", StringComparison.OrdinalIgnoreCase))
                ValidateExclusionCriteria(request.Criteria);
        }

        var entity = await _db.ArchiveRules
            .FirstOrDefaultAsync(
                r => r.Id == ruleId && r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId,
                cancellationToken);

        if (entity is null)
        {
            _logger.LogDebug("Archive rule {RuleId} not found for update", ruleId);
            return null;
        }

        if (request.Name is not null) entity.Name = request.Name;
        if (request.RuleType is not null) entity.RuleType = request.RuleType.ToLowerInvariant();
        if (request.Criteria is not null) entity.Criteria = request.Criteria;
        if (request.TargetTier is not null) entity.TargetTier = request.TargetTier;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated archive rule {RuleId} for tenant {TenantId}",
            ruleId, tenantId);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ArchiveRules
            .FirstOrDefaultAsync(
                r => r.Id == ruleId && r.ClientTenantId == tenantId && r.MspOrgId == mspOrgId,
                cancellationToken);

        if (entity is null)
        {
            _logger.LogDebug("Archive rule {RuleId} not found for deletion", ruleId);
            return false;
        }

        // Soft delete: mark as inactive
        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Soft-deleted archive rule {RuleId} for tenant {TenantId}",
            ruleId, tenantId);

        return true;
    }

    private static void ValidateJsonCriteria(string criteria)
    {
        try
        {
            using var doc = JsonDocument.Parse(criteria);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Criteria must be a JSON object.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("Criteria must be valid JSON.");
        }
    }

    private static readonly HashSet<string> ValidExclusionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "libraryPath", "folderPath", "fileTypes", "complianceTags"
    };

    private static void ValidateExclusionCriteria(string criteria)
    {
        using var doc = JsonDocument.Parse(criteria);
        var root = doc.RootElement;

        var hasValidField = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (!ValidExclusionFields.Contains(prop.Name))
                throw new ArgumentException($"Unknown exclusion criteria field '{prop.Name}'. Valid fields: {string.Join(", ", ValidExclusionFields)}");
            hasValidField = true;
        }

        if (!hasValidField)
            throw new ArgumentException("Exclusion rule must specify at least one criteria field (libraryPath, folderPath, fileTypes, or complianceTags).");

        // Validate fileTypes is an array of strings if present
        if (root.TryGetProperty("fileTypes", out var fileTypes) && fileTypes.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("fileTypes must be a JSON array.");

        // Validate complianceTags is an array of strings if present
        if (root.TryGetProperty("complianceTags", out var tags) && tags.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("complianceTags must be a JSON array.");
    }

    private static JsonElement ParseExclusionCriteria(string criteriaJson)
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

    private static ArchiveRuleDto MapToDto(ArchiveRule entity)
    {
        return new ArchiveRuleDto
        {
            Id = entity.Id,
            ClientTenantId = entity.ClientTenantId,
            Name = entity.Name,
            RuleType = entity.RuleType,
            Criteria = entity.Criteria,
            TargetTier = entity.TargetTier,
            IsActive = entity.IsActive,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
