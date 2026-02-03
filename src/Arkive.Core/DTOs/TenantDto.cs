using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
