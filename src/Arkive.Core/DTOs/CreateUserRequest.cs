using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class CreateUserRequest
{
    public string EntraIdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
