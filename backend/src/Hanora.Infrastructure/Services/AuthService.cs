using System.Net;
using System.Security.Cryptography;
using System.Text;
using Hanora.Application.Abstractions;
using Hanora.Application.Auth;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hanora.Infrastructure.Services;

public sealed class AuthService(
    HanoraDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService,
    JwtOptions jwtOptions,
    IAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult?> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await AuditLoginFailureAsync(ipAddress, "invalid_credentials", cancellationToken);
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            await AuditLoginFailureAsync(ipAddress, "invalid_credentials", cancellationToken);
            return null;
        }

        var now = timeProvider.GetUtcNow();

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var (refreshToken, rawRefreshToken) = CreateRefreshToken(user.Id, ipAddress, now);
        dbContext.RefreshTokens.Add(refreshToken);

        await auditWriter.WriteAsync(
            user.Id,
            "auth.login_succeeded",
            "user",
            user.Id.ToString(),
            null,
            ipAddress,
            cancellationToken);

        logger.LogInformation("Login berhasil untuk user {UserId}.", user.Id);
        return BuildResult(user, refreshToken, rawRefreshToken);
    }

    public async Task<AuthResult?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = HashToken(refreshToken);
        var stored = await dbContext.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        if (stored.RevokedAt is not null)
        {
            // Token lama dipakai ulang: cabut semua sesi aktif user tersebut.
            var activeTokens = await dbContext.RefreshTokens
                .Where(x => x.UserId == stored.UserId && x.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var activeToken in activeTokens)
            {
                activeToken.RevokedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await auditWriter.WriteAsync(
                stored.UserId,
                "auth.refresh_reuse_detected",
                "refresh_token",
                stored.Id.ToString(),
                null,
                ipAddress,
                cancellationToken);

            logger.LogWarning("Refresh token yang sudah dicabut dipakai ulang untuk user {UserId}.", stored.UserId);
            return null;
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive)
        {
            return null;
        }

        stored.RevokedAt = now;

        var (newToken, rawNewToken) = CreateRefreshToken(stored.UserId, ipAddress, now);
        stored.ReplacedByTokenId = newToken.Id;
        dbContext.RefreshTokens.Add(newToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(stored.User, newToken, rawNewToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        return user is null
            ? null
            : new UserProfileDto(user.Id, user.Name, user.Email, user.Role.Name, user.MustChangePassword);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return ChangePasswordResult.UserNotFound;
        }

        if (request.NewPassword.Length < 12)
        {
            return ChangePasswordResult.WeakPassword;
        }

        var currentVerification = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (currentVerification == PasswordVerificationResult.Failed)
        {
            return ChangePasswordResult.InvalidCurrentPassword;
        }

        var newVerification = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.NewPassword);

        if (newVerification != PasswordVerificationResult.Failed ||
            string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return ChangePasswordResult.SamePassword;
        }

        var now = timeProvider.GetUtcNow();
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = now;

        var activeTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await auditWriter.WriteAsync(
            userId,
            "auth.password_changed",
            "user",
            userId.ToString(),
            null,
            ipAddress,
            cancellationToken);

        logger.LogInformation("Password diganti untuk user {UserId}.", userId);
        return ChangePasswordResult.Success;
    }

    private async Task AuditLoginFailureAsync(
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken)
    {
        await auditWriter.WriteAsync(
            null,
            "auth.login_failed",
            "auth",
            null,
            new { reason },
            ipAddress,
            cancellationToken);
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(
        Guid userId,
        string? ipAddress,
        DateTimeOffset now)
    {
        var rawToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddDays(jwtOptions.RefreshDays),
            CreatedIp = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = now
        };

        return (entity, rawToken);
    }

    private AuthResult BuildResult(User user, RefreshToken refreshToken, string rawRefreshToken)
    {
        var accessToken = jwtTokenService.CreateAccessToken(user);

        return new AuthResult(
            accessToken.Token,
            accessToken.ExpiresIn,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            new UserProfileDto(
                user.Id,
                user.Name,
                user.Email,
                user.Role.Name,
                user.MustChangePassword));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
