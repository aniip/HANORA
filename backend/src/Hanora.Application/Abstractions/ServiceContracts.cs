using Hanora.Application.Auth;
using Hanora.Application.Common;
using Hanora.Application.Users;
using Hanora.Domain.Entities;

namespace Hanora.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ServiceResult<UserDto>> CreateAsync(
        CreateUserRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<UserDto>> UpdateAsync(
        Guid userId,
        UpdateUserRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<UserDto>> UpdateStatusAsync(
        Guid userId,
        bool isActive,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(User user);

    TableSessionTokenResult CreateTableSession(DiningTable table, TableQrCode qrCode);
}

public sealed record AccessTokenResult(string Token, int ExpiresIn);

public sealed record TableSessionTokenResult(string Token, int ExpiresIn);

public interface IAuditWriter
{
    Task WriteAsync(
        Guid? userId,
        string action,
        string entity,
        string? entityId,
        object? metadata,
        string? ipAddress,
        CancellationToken cancellationToken);
}
