using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hanora.Application.Abstractions;
using Hanora.Application.Auth;
using Hanora.Domain.Constants;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Hanora.Infrastructure.Services;

public sealed class JwtTokenService(JwtOptions options, TimeProvider timeProvider) : IJwtTokenService
{
    public AccessTokenResult CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.AccessMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(ClaimTypes.Role, user.Role.Name),
            new(AuthConstants.MustChangePasswordClaim, user.MustChangePassword ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(encoded, options.AccessMinutes * 60);
    }

    public TableSessionTokenResult CreateTableSession(
        DiningTable table,
        TableQrCode qrCode)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.TableSessionMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, table.Id.ToString()),
            new(AuthConstants.TableIdClaim, table.Id.ToString()),
            new(AuthConstants.QrCodeIdClaim, qrCode.Id.ToString()),
            new(AuthConstants.TableNumberClaim, table.Number),
            new("name", table.Name),
            new(ClaimTypes.Role, RoleNames.Customer),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.TableAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new TableSessionTokenResult(encoded, options.TableSessionMinutes * 60);
    }
}
