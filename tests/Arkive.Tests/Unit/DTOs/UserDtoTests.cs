using Arkive.Core.DTOs;
using Arkive.Core.Enums;

namespace Arkive.Tests.Unit.DTOs;

public class UserDtoTests
{
    [Fact]
    public void UserDto_HasExpectedDefaults()
    {
        var dto = new UserDto();

        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(Guid.Empty, dto.MspOrgId);
        Assert.Equal(string.Empty, dto.EntraIdObjectId);
        Assert.Equal(string.Empty, dto.Email);
        Assert.Equal(string.Empty, dto.DisplayName);
        Assert.Equal(default(UserRole), dto.Role);
        Assert.Equal(default(DateTimeOffset), dto.CreatedAt);
        Assert.Equal(default(DateTimeOffset), dto.UpdatedAt);
    }

    [Fact]
    public void UserDto_PropertiesAreSettable()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var dto = new UserDto
        {
            Id = id,
            MspOrgId = orgId,
            EntraIdObjectId = "entra-object-id",
            Email = "user@example.com",
            DisplayName = "Test User",
            Role = UserRole.MspAdmin,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, dto.Id);
        Assert.Equal(orgId, dto.MspOrgId);
        Assert.Equal("entra-object-id", dto.EntraIdObjectId);
        Assert.Equal("user@example.com", dto.Email);
        Assert.Equal("Test User", dto.DisplayName);
        Assert.Equal(UserRole.MspAdmin, dto.Role);
        Assert.Equal(now, dto.CreatedAt);
        Assert.Equal(now, dto.UpdatedAt);
    }

    [Fact]
    public void CreateUserRequest_HasExpectedDefaults()
    {
        var request = new CreateUserRequest();

        Assert.Equal(string.Empty, request.EntraIdObjectId);
        Assert.Equal(string.Empty, request.Email);
        Assert.Equal(string.Empty, request.DisplayName);
        Assert.Equal(default(UserRole), request.Role);
    }

    [Fact]
    public void CreateUserRequest_PropertiesAreSettable()
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = "entra-id",
            Email = "test@test.com",
            DisplayName = "Test",
            Role = UserRole.MspTech
        };

        Assert.Equal("entra-id", request.EntraIdObjectId);
        Assert.Equal("test@test.com", request.Email);
        Assert.Equal("Test", request.DisplayName);
        Assert.Equal(UserRole.MspTech, request.Role);
    }

    [Fact]
    public void UpdateUserRequest_HasExpectedDefaults()
    {
        var request = new UpdateUserRequest();

        Assert.Equal(default(UserRole), request.Role);
    }

    [Fact]
    public void UpdateUserRequest_PropertiesAreSettable()
    {
        var request = new UpdateUserRequest { Role = UserRole.MspAdmin };

        Assert.Equal(UserRole.MspAdmin, request.Role);
    }
}
