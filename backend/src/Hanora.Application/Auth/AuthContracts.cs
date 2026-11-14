using System.ComponentModel.DataAnnotations;

namespace Hanora.Application.Auth;

public static class AuthConstants
{
    public const string RefreshCookieName = "hanora_refresh";
    public const string SessionMarkerCookieName = "hanora_session";
    public const string RefreshCookiePath = "/api/v1/auth";
    public const string MustChangePasswordClaim = "must_change_password";
    public const string TableIdClaim = "table_id";
    public const string QrCodeIdClaim = "qr_code_id";
    public const string TableNumberClaim = "table_number";
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi.")]
    [MinLength(8, ErrorMessage = "Password minimal 8 karakter.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Password saat ini wajib diisi.")]
    [MaxLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password baru wajib diisi.")]
    [MinLength(12, ErrorMessage = "Password baru minimal 12 karakter.")]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record UserProfileDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool MustChangePassword);

public sealed record AuthResult(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);

public enum ChangePasswordResult
{
    Success,
    UserNotFound,
    InvalidCurrentPassword,
    SamePassword,
    WeakPassword
}
