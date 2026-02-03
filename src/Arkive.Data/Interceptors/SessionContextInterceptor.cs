using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Arkive.Data.Interceptors;

/// <summary>
/// EF Core DbConnectionInterceptor that sets SQL SESSION_CONTEXT with tenant identifiers
/// on every connection open. This enables Azure SQL Row-Level Security (RLS) to automatically
/// filter all queries by the current tenant.
/// </summary>
public class SessionContextInterceptor : DbConnectionInterceptor
{
    private readonly TenantContext _tenantContext;

    public SessionContextInterceptor(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tenantContext.MspOrgId))
        {
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
            return;
        }

        await ExecuteSessionContextCommandAsync(
            connection, "MspOrgId", _tenantContext.MspOrgId, cancellationToken);

        if (!string.IsNullOrEmpty(_tenantContext.ClientTenantId))
        {
            await ExecuteSessionContextCommandAsync(
                connection, "ClientTenantId", _tenantContext.ClientTenantId, cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        if (string.IsNullOrEmpty(_tenantContext.MspOrgId))
        {
            base.ConnectionOpened(connection, eventData);
            return;
        }

        ExecuteSessionContextCommand(connection, "MspOrgId", _tenantContext.MspOrgId);

        if (!string.IsNullOrEmpty(_tenantContext.ClientTenantId))
        {
            ExecuteSessionContextCommand(connection, "ClientTenantId", _tenantContext.ClientTenantId);
        }

        base.ConnectionOpened(connection, eventData);
    }

    private static async Task ExecuteSessionContextCommandAsync(
        DbConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        ConfigureSessionContextCommand(cmd, key, value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ExecuteSessionContextCommand(DbConnection connection, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        ConfigureSessionContextCommand(cmd, key, value);
        cmd.ExecuteNonQuery();
    }

    private static void ConfigureSessionContextCommand(DbCommand cmd, string key, string value)
    {
        cmd.CommandText = $"EXEC sp_set_session_context @key = N'{key}', @value = @val, @read_only = 1";

        var param = cmd.CreateParameter();
        param.ParameterName = "@val";
        param.DbType = DbType.String;
        param.Size = 128;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
