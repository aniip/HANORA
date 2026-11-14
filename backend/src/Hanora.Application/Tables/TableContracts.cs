using System.ComponentModel.DataAnnotations;

namespace Hanora.Application.Tables;

public sealed record TableInfoDto(
    Guid Id,
    string Number,
    string Name,
    short Capacity);

public sealed record TableDto(
    Guid Id,
    string Number,
    string Name,
    short Capacity,
    bool IsActive,
    string? QrUrl,
    DateTimeOffset? QrCreatedAt);

public sealed class TableRequest
{
    [Required(ErrorMessage = "Nomor meja wajib diisi.")]
    [MinLength(1, ErrorMessage = "Nomor meja wajib diisi.")]
    [MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nama meja wajib diisi.")]
    [MinLength(1, ErrorMessage = "Nama meja wajib diisi.")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "Kapasitas meja antara 1 dan 50.")]
    public short Capacity { get; set; } = 2;

    public bool IsActive { get; set; } = true;
}

public sealed record TableSessionDto(
    TableInfoDto Table,
    string TableSessionToken,
    int ExpiresIn);

public sealed record TableQrFile(
    byte[] Content,
    string ContentType,
    string FileName);
