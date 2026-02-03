using Microsoft.Azure.Functions.Worker;

namespace Arkive.Functions.Extensions;

public static class FunctionContextExtensions
{
    public static string GetUserId(this FunctionContext context)
        => context.Items.TryGetValue("UserId", out var value) ? value as string ?? string.Empty : string.Empty;

    public static string GetEntraObjectId(this FunctionContext context)
        => context.Items.TryGetValue("EntraObjectId", out var value) ? value as string ?? string.Empty : string.Empty;

    public static string GetMspOrgId(this FunctionContext context)
        => context.Items.TryGetValue("MspOrgId", out var value) ? value as string ?? string.Empty : string.Empty;

    public static string GetUserRole(this FunctionContext context)
        => context.Items.TryGetValue("UserRole", out var value) ? value as string ?? string.Empty : string.Empty;

    public static List<string> GetUserRoles(this FunctionContext context)
        => context.Items.TryGetValue("UserRoles", out var value) ? value as List<string> ?? [] : [];

    public static string GetUserName(this FunctionContext context)
        => context.Items.TryGetValue("UserName", out var value) ? value as string ?? string.Empty : string.Empty;

    public static string GetUserEmail(this FunctionContext context)
        => context.Items.TryGetValue("UserEmail", out var value) ? value as string ?? string.Empty : string.Empty;
}
