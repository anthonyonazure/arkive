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

public class UserEndpointsTests
{
    private readonly IUserService _service;
    private readonly UserEndpoints _endpoints;
    private readonly Guid _orgId = Guid.NewGuid();

    public UserEndpointsTests()
    {
        _service = Substitute.For<IUserService>();
        var logger = NullLogger<UserEndpoints>.Instance;
        _endpoints = new UserEndpoints(_service, logger);
    }

    private FunctionContext CreateMockContext(string role)
    {
        var context = Substitute.For<FunctionContext>();
        var items = new Dictionary<object, object>
        {
            { "UserRole", role },
            { "MspOrgId", _orgId.ToString() }
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

    // POST /v1/users tests

    [Fact]
    public async Task CreateUser_WithValidRequest_AsMspAdmin_Returns201()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var requestBody = new CreateUserRequest
        {
            EntraIdObjectId = "entra-obj-001",
            Email = "newuser@test.com",
            DisplayName = "New User",
            Role = UserRole.MspTech
        };
        var req = CreateJsonRequest(requestBody);

        var expectedDto = new UserDto
        {
            Id = Guid.NewGuid(),
            MspOrgId = _orgId,
            EntraIdObjectId = "entra-obj-001",
            Email = "newuser@test.com",
            DisplayName = "New User",
            Role = UserRole.MspTech
        };
        _service.CreateAsync(Arg.Any<CreateUserRequest>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var result = await _endpoints.CreateUser(req, context);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Contains("/api/v1/users/", createdResult.Location);
        var response = Assert.IsType<ApiResponse<UserDto>>(createdResult.Value);
        Assert.Equal("newuser@test.com", response.Data.Email);
    }

    [Fact]
    public async Task CreateUser_AsPlatformAdmin_Returns201()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var requestBody = new CreateUserRequest
        {
            EntraIdObjectId = "entra-obj-002",
            Email = "admin@test.com",
            DisplayName = "Admin User",
            Role = UserRole.MspAdmin
        };
        var req = CreateJsonRequest(requestBody);

        var expectedDto = new UserDto
        {
            Id = Guid.NewGuid(),
            MspOrgId = _orgId,
            Email = "admin@test.com",
            Role = UserRole.MspAdmin
        };
        _service.CreateAsync(Arg.Any<CreateUserRequest>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var result = await _endpoints.CreateUser(req, context);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("FORBIDDEN", response.Error.Code);
    }

    [Fact]
    public async Task CreateUser_MspAdminCreatingPlatformAdmin_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var requestBody = new CreateUserRequest
        {
            EntraIdObjectId = "entra-obj-003",
            Email = "escalate@test.com",
            DisplayName = "Escalation User",
            Role = UserRole.PlatformAdmin
        };
        var req = CreateJsonRequest(requestBody);

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithEmptyEntraIdObjectId_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = "",
            Email = "valid@test.com",
            DisplayName = "Valid",
            Role = UserRole.MspTech
        });

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithEntraIdExceeding36Chars_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = new string('A', 37),
            Email = "valid@test.com",
            DisplayName = "Valid",
            Role = UserRole.MspTech
        });

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithEmptyEmail_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = "entra-valid",
            Email = "",
            DisplayName = "Valid",
            Role = UserRole.MspTech
        });

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithEmptyDisplayName_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = "entra-valid",
            Email = "valid@test.com",
            DisplayName = "",
            Role = UserRole.MspTech
        });

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEntraId_Returns409()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = "entra-dup",
            Email = "dup@test.com",
            DisplayName = "Dup User",
            Role = UserRole.MspTech
        });

        _service.CreateAsync(Arg.Any<CreateUserRequest>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("Unique constraint violation", new Exception()));

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("CONFLICT", response.Error.Code);
    }

    // GET /v1/users tests

    [Fact]
    public async Task ListUsers_AsMspAdmin_Returns200()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();

        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), Email = "user1@test.com", Role = UserRole.MspTech },
            new() { Id = Guid.NewGuid(), Email = "user2@test.com", Role = UserRole.MspAdmin }
        };
        _service.GetAllByOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(users);

        var result = await _endpoints.ListUsers(req, context);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<UserDto>>>(okResult.Value);
        Assert.Equal(2, response.Data.Count);
    }

    [Fact]
    public async Task ListUsers_AsPlatformAdmin_ReturnsAllUsers()
    {
        var context = CreateMockContext(AppRoles.PlatformAdmin);
        var req = CreateEmptyRequest();

        var allUsers = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), Email = "a@test.com" },
            new() { Id = Guid.NewGuid(), Email = "b@test.com" },
            new() { Id = Guid.NewGuid(), Email = "c@test.com" }
        };
        _service.GetAllAsync(Arg.Any<CancellationToken>()).Returns(allUsers);

        var result = await _endpoints.ListUsers(req, context);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<UserDto>>>(okResult.Value);
        Assert.Equal(3, response.Data.Count);
    }

    [Fact]
    public async Task ListUsers_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.ListUsers(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // GET /v1/users/{id} tests

    [Fact]
    public async Task GetUser_WithExistingUser_Returns200()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();
        var userId = Guid.NewGuid();

        var userDto = new UserDto
        {
            Id = userId,
            Email = "found@test.com",
            DisplayName = "Found User",
            Role = UserRole.MspTech
        };
        _service.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(userDto);

        var result = await _endpoints.GetUser(req, context, userId.ToString());

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.Equal("found@test.com", response.Data.Email);
    }

    [Fact]
    public async Task GetUser_WithNonExistentUser_Returns404()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();
        var userId = Guid.NewGuid();

        _service.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        var result = await _endpoints.GetUser(req, context, userId.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal("NOT_FOUND", response.Error.Code);
    }

    [Fact]
    public async Task GetUser_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.GetUser(req, context, Guid.NewGuid().ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // PUT /v1/users/{id} tests

    [Fact]
    public async Task UpdateUser_WithValidRoleUpdate_Returns200()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var userId = Guid.NewGuid();
        var req = CreateJsonRequest(new UpdateUserRequest { Role = UserRole.MspAdmin });

        var updatedDto = new UserDto
        {
            Id = userId,
            Email = "updated@test.com",
            Role = UserRole.MspAdmin
        };
        _service.UpdateRoleAsync(userId, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>()).Returns(updatedDto);

        var result = await _endpoints.UpdateUser(req, context, userId.ToString());

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.Equal(UserRole.MspAdmin, response.Data.Role);
    }

    [Fact]
    public async Task UpdateUser_WithNonExistentUser_Returns404()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var userId = Guid.NewGuid();
        var req = CreateJsonRequest(new UpdateUserRequest { Role = UserRole.MspTech });

        _service.UpdateRoleAsync(userId, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        var result = await _endpoints.UpdateUser(req, context, userId.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_MspAdminAssigningPlatformAdmin_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var userId = Guid.NewGuid();
        var req = CreateJsonRequest(new UpdateUserRequest { Role = UserRole.PlatformAdmin });

        var result = await _endpoints.UpdateUser(req, context, userId.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.UpdateUser(req, context, Guid.NewGuid().ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // DELETE /v1/users/{id} tests

    [Fact]
    public async Task DeleteUser_WithExistingUser_Returns204()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();
        var userId = Guid.NewGuid();

        _service.DeleteAsync(userId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _endpoints.DeleteUser(req, context, userId.ToString());

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentUser_Returns404()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();
        var userId = Guid.NewGuid();

        _service.DeleteAsync(userId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _endpoints.DeleteUser(req, context, userId.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsMspTech_Returns403()
    {
        var context = CreateMockContext(AppRoles.MspTech);
        var req = CreateEmptyRequest();

        var result = await _endpoints.DeleteUser(req, context, Guid.NewGuid().ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // Invalid GUID route parameter tests

    [Fact]
    public async Task GetUser_WithInvalidGuid_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();

        var result = await _endpoints.GetUser(req, context, "not-a-guid");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidGuid_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new UpdateUserRequest { Role = UserRole.MspTech });

        var result = await _endpoints.UpdateUser(req, context, "not-a-guid");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithInvalidGuid_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateEmptyRequest();

        var result = await _endpoints.DeleteUser(req, context, "not-a-guid");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    // Email max length test

    [Fact]
    public async Task CreateUser_WithEmailExceeding320Chars_Returns400()
    {
        var context = CreateMockContext(AppRoles.MspAdmin);
        var req = CreateJsonRequest(new CreateUserRequest
        {
            EntraIdObjectId = "entra-valid",
            Email = new string('a', 321),
            DisplayName = "Valid",
            Role = UserRole.MspTech
        });

        var result = await _endpoints.CreateUser(req, context);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }
}
