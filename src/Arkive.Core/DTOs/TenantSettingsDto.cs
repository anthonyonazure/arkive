using System.Text.Json;

namespace Arkive.Core.DTOs;

/// <summary>
/// Request to update tenant-level settings.
/// Uses JsonElement? to distinguish between "not provided" and "explicitly null" for PATCH semantics.
/// </summary>
public class UpdateTenantSettingsRequest
{
    /// <summary>
    /// Number of days before unresponded approval requests are auto-approved.
    /// Absent from JSON = do not change.
    /// null = never auto-approve (block indefinitely).
    /// 0 = immediate auto-approve (skip approval waiting).
    /// 1-365 = wait N days then auto-approve.
    /// </summary>
    public JsonElement? AutoApprovalDays { get; set; }

    /// <summary>
    /// Extracts the typed AutoApprovalDays value from the JsonElement.
    /// Returns (true, value) if the property was present (value may be null for explicit null).
    /// Returns (false, null) if the property was absent.
    /// </summary>
    public (bool WasProvided, int? Value) GetAutoApprovalDays()
    {
        if (!AutoApprovalDays.HasValue)
            return (false, null);

        if (AutoApprovalDays.Value.ValueKind == JsonValueKind.Null)
            return (true, null);

        if (AutoApprovalDays.Value.ValueKind == JsonValueKind.Number
            && AutoApprovalDays.Value.TryGetInt32(out var days))
            return (true, days);

        return (false, null);
    }
}

/// <summary>
/// Current tenant settings response.
/// </summary>
public class TenantSettingsDto
{
    public Guid TenantId { get; set; }
    public int? AutoApprovalDays { get; set; }
    public bool ReviewFlagged { get; set; }
}
