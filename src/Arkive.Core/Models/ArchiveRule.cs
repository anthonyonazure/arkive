namespace Arkive.Core.Models;

public class ArchiveRule
{
    public Guid Id { get; set; }
    public Guid ClientTenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Criteria { get; set; } = "{}";
    public string TargetTier { get; set; } = "Cool";
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ClientTenant ClientTenant { get; set; } = null!;
}
