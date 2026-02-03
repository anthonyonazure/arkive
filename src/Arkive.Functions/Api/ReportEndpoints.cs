using System.Security.Cryptography;
using System.Text.Json;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class ReportEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly IFleetAnalyticsService _analyticsService;
    private readonly ISavingsSnapshotService _snapshotService;
    private readonly ILogger<ReportEndpoints> _logger;

    public ReportEndpoints(
        ArkiveDbContext db,
        IFleetAnalyticsService analyticsService,
        ISavingsSnapshotService snapshotService,
        ILogger<ReportEndpoints> logger)
    {
        _db = db;
        _analyticsService = analyticsService;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a shareable report snapshot with a public token (30-day expiry).
    /// </summary>
    [Function("CreateReportSnapshot")]
    public async Task<IActionResult> CreateSnapshot(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/reports/snapshots")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        CreateReportSnapshotRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateReportSnapshotRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                req.HttpContext.RequestAborted);
        }
        catch
        {
            return ResponseEnvelopeHelper.BadRequest("Invalid request body.", context.InvocationId);
        }

        if (body == null || body.TenantId == Guid.Empty)
            return ResponseEnvelopeHelper.BadRequest("tenantId is required.", context.InvocationId);

        try
        {
            // Verify tenant belongs to this org
            var tenant = await _db.ClientTenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == body.TenantId && t.MspOrgId == parsedOrgId,
                    req.HttpContext.RequestAborted);

            if (tenant == null)
                return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);

            // Fetch current analytics and trends
            var analytics = await _analyticsService.GetTenantAnalyticsAsync(body.TenantId, parsedOrgId, req.HttpContext.RequestAborted);
            var trends = await _snapshotService.GetTrendsAsync(parsedOrgId, body.TenantId, 12, req.HttpContext.RequestAborted);

            // Generate URL-safe token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            var reportData = new { analytics, trends };
            var reportJson = JsonSerializer.Serialize(reportData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            var snapshot = new ReportSnapshot
            {
                MspOrgId = parsedOrgId,
                ClientTenantId = body.TenantId,
                Token = token,
                TenantName = tenant.DisplayName,
                ReportJson = reportJson,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };

            _db.ReportSnapshots.Add(snapshot);
            await _db.SaveChangesAsync(req.HttpContext.RequestAborted);

            var baseUrl = $"{req.Scheme}://{req.Host}";
            var response = new ReportSnapshotResponse
            {
                Token = token,
                Url = $"{baseUrl}/shared/{token}",
                ExpiresAt = snapshot.ExpiresAt,
            };

            return ResponseEnvelopeHelper.Created($"/v1/reports/snapshots/{token}", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create report snapshot for tenant {TenantId}", body.TenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to create report snapshot.",
                context.InvocationId);
        }
    }

    /// <summary>
    /// Public endpoint — retrieves a shared report by token. No authentication required.
    /// </summary>
    [Function("GetSharedReport")]
    public async Task<IActionResult> GetSharedReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/reports/snapshots/{token}")] HttpRequest req,
        string token,
        FunctionContext context)
    {
        if (string.IsNullOrEmpty(token))
            return ResponseEnvelopeHelper.BadRequest("Token is required.", context.InvocationId);

        try
        {
            var snapshot = await _db.ReportSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Token == token, req.HttpContext.RequestAborted);

            if (snapshot == null)
                return ResponseEnvelopeHelper.NotFound("Report not found.", context.InvocationId);

            if (snapshot.ExpiresAt < DateTimeOffset.UtcNow)
                return ResponseEnvelopeHelper.Error(
                    System.Net.HttpStatusCode.Gone,
                    "EXPIRED",
                    "This shared report has expired.",
                    context.InvocationId);

            // Deserialize the stored JSON back to dynamic objects
            var reportData = JsonSerializer.Deserialize<JsonElement>(snapshot.ReportJson);

            var response = new SharedReportData
            {
                TenantName = snapshot.TenantName,
                GeneratedAt = snapshot.CreatedAt,
                ExpiresAt = snapshot.ExpiresAt,
                Analytics = reportData.TryGetProperty("analytics", out var analytics) ? analytics : null,
                Trends = reportData.TryGetProperty("trends", out var trends) ? trends : null,
            };

            return ResponseEnvelopeHelper.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve shared report for token {Token}", token);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to retrieve shared report.",
                context.InvocationId);
        }
    }
}
