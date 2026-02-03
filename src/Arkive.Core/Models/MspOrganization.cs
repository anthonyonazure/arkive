using Arkive.Core.Enums;

namespace Arkive.Core.Models;

public class MspOrganization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
    public string EntraIdTenantId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; } = [];
    public ICollection<ClientTenant> ClientTenants { get; set; } = [];
}
