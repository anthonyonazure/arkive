namespace Arkive.Core.DTOs;

public class ConsentCallbackRequest
{
    public bool AdminConsent { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}
