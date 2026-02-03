namespace Arkive.Core.DTOs;

public class CreateMspOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string EntraIdTenantId { get; set; } = string.Empty;
}
