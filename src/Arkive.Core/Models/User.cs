using Arkive.Core.Enums;

namespace Arkive.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public string EntraIdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.MspTech;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public MspOrganization MspOrganization { get; set; } = null!;
}
