using Arkive.Core.Configuration;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Arkive.Data.Interceptors;
using Arkive.Functions.Middleware;
using Arkive.Functions.Services;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Key Vault configuration provider (overlays secrets onto IConfiguration)
var vaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(vaultUri))
{
    var credential = new DefaultAzureCredential();
    var vaultEndpoint = new Uri(vaultUri);

    builder.Configuration.AddAzureKeyVault(vaultEndpoint, credential);
    builder.Services.AddSingleton(new SecretClient(vaultEndpoint, credential));
    builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
}

// Authentication middleware (FIRST - validates JWT, sets claims on FunctionContext)
builder.UseMiddleware<AuthenticationMiddleware>();

// Tenant context middleware (SECOND - reads claims, populates scoped TenantContext)
builder.UseMiddleware<TenantContextMiddleware>();

// Entra ID configuration
builder.Services.Configure<EntraIdOptions>(
    builder.Configuration.GetSection(EntraIdOptions.SectionName));

// Key Vault configuration
builder.Services.Configure<KeyVaultOptions>(
    builder.Configuration.GetSection(KeyVaultOptions.SectionName));

builder.Services.AddMemoryCache();

// MSP Organization management
builder.Services.AddScoped<IMspOrganizationService, MspOrganizationService>();

// User management
builder.Services.AddScoped<IUserService, UserService>();

// Tenant onboarding
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISiteDiscoveryService, SiteDiscoveryService>();
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

// Storage scanning
builder.Services.AddScoped<IScanService, ScanService>();

// Audit log indexing
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Fleet analytics
builder.Services.AddScoped<IFleetAnalyticsService, FleetAnalyticsService>();

// Archive rules
builder.Services.AddScoped<IArchiveRuleService, ArchiveRuleService>();

// Rule evaluation engine
builder.Services.AddScoped<IRuleEvaluationService, RuleEvaluationService>();

// Archive execution pipeline
builder.Services.AddScoped<IArchiveService, ArchiveService>();

// Audit trail service (compliance logging)
builder.Services.AddScoped<IAuditService, AuditService>();

// Savings snapshot service (monthly trend data)
builder.Services.AddScoped<ISavingsSnapshotService, SavingsSnapshotService>();

// Teams notification service (Bot Framework Adaptive Cards)
builder.Services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();

// Approval action handler (Teams card responses)
builder.Services.AddScoped<IApprovalActionHandler, ApprovalActionHandler>();

// Azure Blob Storage client for archive operations
var blobStorageConnection = builder.Configuration["BlobStorageConnection"];
if (!string.IsNullOrEmpty(blobStorageConnection))
{
    if (blobStorageConnection.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddSingleton(new BlobServiceClient(new Uri(blobStorageConnection), new DefaultAzureCredential()));
    else
        builder.Services.AddSingleton(new BlobServiceClient(blobStorageConnection));
}
else
{
    // Local dev: Use Azurite connection string for local blob storage emulation
    builder.Services.AddSingleton(new BlobServiceClient("UseDevelopmentStorage=true"));
}

// Service Bus client for publishing scan jobs and events
// Required for ScheduledScanTrigger to publish messages; queue triggers use ServiceBusConnection binding separately
var serviceBusConnection = builder.Configuration["ServiceBusConnection"];
if (!string.IsNullOrEmpty(serviceBusConnection))
{
    if (serviceBusConnection.Contains(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection, new DefaultAzureCredential()));
    else
        builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
}
else
{
    // Local dev: ServiceBusClient not available — timer trigger will log warning and skip publishing.
    // Queue triggers (ScanJobProcessor) use the ServiceBusConnection binding directly and are unaffected.
}

// Tenant context (scoped - one per request)
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<SessionContextInterceptor>();

// Database context
// When Key Vault is configured, SqlConnection comes from Key Vault via IConfiguration overlay.
// When Key Vault is NOT configured (local dev), falls back to local.settings.json value.
var connectionString = builder.Configuration["SqlConnection"]
    ?? "Server=(localdb)\\mssqllocaldb;Database=Arkive;Trusted_Connection=true;TrustServerCertificate=true;";

builder.Services.AddDbContext<ArkiveDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(sp.GetRequiredService<SessionContextInterceptor>());
});

// Application Insights telemetry
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
