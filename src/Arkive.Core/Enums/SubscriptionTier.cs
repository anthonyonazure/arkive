using System.Text.Json.Serialization;

namespace Arkive.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionTier
{
    Free,
    Starter,
    Professional,
    Enterprise
}
