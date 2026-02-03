using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public string EntraIdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
