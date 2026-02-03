namespace Arkive.Core.DTOs;

public class CreateTenantRequest
{
    public string M365TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
