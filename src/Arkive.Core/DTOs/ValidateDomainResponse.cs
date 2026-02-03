namespace Arkive.Core.DTOs;

public class ValidateDomainResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
}
