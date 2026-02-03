using System.Text;
using System.Text.Json;
using Arkive.Core.Constants;
using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Functions.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Arkive.Tests.Unit.Api;

public class OrganizationEndpointsTests
{
    private readonly IMspOrganizationService _service;
    private readonly OrganizationEndpoints _endpoints;

    public OrganizationEndpointsTests()
    {
        _service = Substitute.For<IMspOrganizationService>();
        var logger = NullLogger<OrganizationEndpoints>.Instance;
        _endpoints = new OrganizationEndpoints(_service, logger);
    }

    private static FunctionContext CreateMockContext(string role)
    {
        var context = Substitute.For<FunctionContext>();
        var items = new Dictionary<object, object>
        {
            { "UserRole", role }
        };
        context.Items.Returns(items);
        context.InvocationId.Returns(Guid.NewGuid().ToString());
        return context;
    }

    private static HttpRequest CreateJsonRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static HttpRequest CreateEmptyRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // POST /v1/organizations tests

    [Fact]
    public async Task CreateOrganization_WithValidRequest_Returns201()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var requestBody = new CreateMspOrganizationRequest
        {
            Name = "New Org",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001"
        };
        var req = CreateJsonRequest(requestBody);

        var expectedDto = new MspOrganizationDto
        {
            Id = Guid.NewGuid(),
            Name = "New Org",
            SubscriptionTier = SubscriptionTier.Starter,
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001",
            UserCount = 0,
            TenantCount = 0
        };
        _service.CreateAsync(Arg.Any<CreateMspOrganizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var result = await _endpoints.CreateOrganization(req, context);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Contains("/api/v1/organizations/", createdResult.Location);
        var response = Assert.IsType<ApiResponse<MspOrganizationDto>>(createdResult.Value);
        Assert.Equal("New Org", response.Data.Name);
        Assert.Equal(SubscriptionTier.Starter, response.Data.SubscriptionTier);
    }

    [Fact]
    public async Task CreateOrganization_AsNonPlatformAdmin_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = "Org",
            EntraIdTenantId = "tenant-id"
        });

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("FORBIDDEN", response.Error.Code);
    }

    [Fact]
    public async Task CreateOrganization_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_WithEmptyName_Returns400()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = "",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001"
        });

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("BAD_REQUEST", response.Error.Code);
    }

    [Fact]
    public async Task CreateOrganization_WithNameExceeding200Chars_Returns400()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = new string('A', 201),
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001"
        });

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_WithEmptyEntraIdTenantId_Returns400()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = "Valid Name",
            EntraIdTenantId = ""
        });

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_WithEntraIdExceeding36Chars_Returns400()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = "Valid Name",
            EntraIdTenantId = new string('A', 37)
        });

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_WithDuplicateEntraId_Returns409()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateJsonRequest(new CreateMspOrganizationRequest
        {
            Name = "Duplicate Org",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001"
        });

        _service.CreateAsync(Arg.Any<CreateMspOrganizationRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("Unique constraint violation", new Exception()));

        var result = await _endpoints.CreateOrganization(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("CONFLICT", response.Error.Code);
    }

    // GET /v1/organizations tests

    [Fact]
    public async Task ListOrganizations_AsPlatformAdmin_Returns200()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateEmptyRequest();

        var orgs = new List<MspOrganizationDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Org A", SubscriptionTier = SubscriptionTier.Starter, UserCount = 3, TenantCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "Org B", SubscriptionTier = SubscriptionTier.Professional, UserCount = 10, TenantCount = 20 }
        };
        _service.GetAllAsync(Arg.Any<CancellationToken>()).Returns(orgs);

        var result = await _endpoints.ListOrganizations(req, context);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MspOrganizationDto>>>(okResult.Value);
        Assert.Equal(2, response.Data.Count);
    }

    [Fact]
    public async Task ListOrganizations_AsMspAdmin_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();

        var result = await _endpoints.ListOrganizations(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("FORBIDDEN", response.Error.Code);
    }

    [Fact]
    public async Task ListOrganizations_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.ListOrganizations(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }
}
