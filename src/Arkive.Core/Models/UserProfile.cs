namespace Arkive.Core.Models;

public class UserProfile
{
    public string EntraObjectId { get; set; } = string.Empty;
    public string MspOrgId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}
