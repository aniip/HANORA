using System.ComponentModel.DataAnnotations;

namespace Hanora.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Nama wajib diisi.")]
    [MinLength(2, ErrorMessage = "Nama minimal 2 karakter.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi.")]
    [MinLength(12, ErrorMessage = "Password minimal 12 karakter.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role wajib diisi.")]
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    [Required(ErrorMessage = "Nama wajib diisi.")]
    [MinLength(2, ErrorMessage = "Nama minimal 2 karakter.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role wajib diisi.")]
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}
