using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hanora.Api.Security;
using Hanora.Application.Abstractions;
using Hanora.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, GetIpAddress(), cancellationToken);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Login gagal",
                detail: "Email atau password salah.");
        }

        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(new
        {
            accessToken = result.AccessToken,
            expiresIn = result.ExpiresIn,
            user = result.User
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthConstants.RefreshCookieName];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sesi berakhir",
                detail: "Silakan login kembali.");
        }

        var result = await authService.RefreshAsync(refreshToken, GetIpAddress(), cancellationToken);

        if (result is null)
        {
            ClearRefreshCookie();
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sesi berakhir",
                detail: "Silakan login kembali.");
        }

        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(new
        {
            accessToken = result.AccessToken,
            expiresIn = result.ExpiresIn,
            user = result.User
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthConstants.RefreshCookieName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.LogoutAsync(refreshToken, cancellationToken);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Tidak terautentikasi",
                detail: "Token tidak valid.");
        }

        var profile = await authService.GetProfileAsync(userId.Value, cancellationToken);

        return profile is null
            ? Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Tidak terautentikasi",
                detail: "Akun tidak ditemukan atau tidak aktif.")
            : Ok(profile);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Tidak terautentikasi",
                detail: "Token tidak valid.");
        }

        var result = await authService.ChangePasswordAsync(
            userId.Value, request, GetIpAddress(), cancellationToken);

        if (result == ChangePasswordResult.Success)
        {
            ClearRefreshCookie();
            return NoContent();
        }

        return result switch
        {
            ChangePasswordResult.UserNotFound => Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Akun tidak ditemukan",
                detail: "Silakan login kembali."),
            ChangePasswordResult.InvalidCurrentPassword => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Password saat ini salah",
                detail: "Periksa kembali password yang Anda masukkan."),
            ChangePasswordResult.SamePassword => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Password tidak berubah",
                detail: "Password baru harus berbeda dari password saat ini."),
            ChangePasswordResult.WeakPassword => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Password terlalu lemah",
                detail: "Password baru minimal 12 karakter."),
            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Ganti password gagal",
                detail: "Permintaan tidak dapat diproses.")
        };
    }

    private Guid? GetUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        var secure = environment.IsProduction() || Request.IsHttps;

        Response.Cookies.Append(AuthConstants.RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = AuthConstants.RefreshCookiePath,
            Expires = expiresAt,
            IsEssential = true
        });

        // Penanda non-sensitif agar frontend tidak memanggil /refresh saat belum
        // pernah login. Tidak berisi token dan dapat dibaca JavaScript.
        Response.Cookies.Append(AuthConstants.SessionMarkerCookieName, "1", new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true
        });
    }

    private void ClearRefreshCookie()
    {
        var secure = environment.IsProduction() || Request.IsHttps;

        Response.Cookies.Delete(AuthConstants.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = AuthConstants.RefreshCookiePath
        });

        Response.Cookies.Delete(AuthConstants.SessionMarkerCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}
