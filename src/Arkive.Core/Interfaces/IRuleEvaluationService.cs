using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IRuleEvaluationService
{
    /// <summary>
    /// Evaluates whether a file should be archived or excluded based on active rules.
    /// Exclusion rules always take precedence over archive rules.
    /// </summary>
    Task<RuleEvaluationResultDto> EvaluateFileAsync(
        Guid tenantId,
        Guid mspOrgId,
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates all files for a tenant against active rules.
    /// Returns only files that match archive rules and are NOT excluded.
    /// </summary>
    Task<IReadOnlyList<RuleEvaluationResultDto>> EvaluateAllFilesAsync(
        Guid tenantId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the scope (affected file count and size) for an exclusion rule.
    /// </summary>
    Task<ExclusionScopeDto> GetExclusionScopeAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the impact of an existing rule (by ruleId) against tenant files,
    /// excluding files matched by active exclusion rules.
    /// </summary>
    Task<DryRunPreviewDto> PreviewRuleAsync(
        Guid tenantId,
        Guid ruleId,
        Guid mspOrgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the impact of ad-hoc rule criteria (before saving),
    /// excluding files matched by active exclusion rules.
    /// </summary>
    Task<DryRunPreviewDto> PreviewAdHocRuleAsync(
        Guid tenantId,
        Guid mspOrgId,
        DryRunPreviewRequest request,
        CancellationToken cancellationToken = default);
}
