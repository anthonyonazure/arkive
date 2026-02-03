using System.Text.Json.Serialization;

namespace Arkive.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantStatus
{
    Pending,
    Connected,
    Disconnecting,
    Disconnected,
    Error
}
