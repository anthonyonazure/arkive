using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class MspOrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SubscriptionTier SubscriptionTier { get; set; }
    public string EntraIdTenantId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int UserCount { get; set; }
    public int TenantCount { get; set; }
}
