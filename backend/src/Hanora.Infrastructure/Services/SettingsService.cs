using System.Text.Json;
using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Common;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class SettingsService(
    HanoraDbContext dbContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : ISettingsService
{
    private const string NameKey = "business.name";
    private const string TaglineKey = "business.tagline";
    private const string DescriptionKey = "business.description";
    private const string AddressKey = "business.address";
    private const string PhoneKey = "business.phone";
    private const string InstagramKey = "business.instagram";
    private const string OpeningHoursKey = "business.opening_hours";
    private const string MapsUrlKey = "business.maps_url";

    private static readonly string[] BusinessKeys =
    [
        NameKey,
        TaglineKey,
        DescriptionKey,
        AddressKey,
        PhoneKey,
        InstagramKey,
        OpeningHoursKey,
        MapsUrlKey
    ];

    public Task<SiteSettingsDto> GetPublicAsync(CancellationToken cancellationToken) =>
        ReadAsync(cancellationToken);

    public Task<SiteSettingsDto> GetOwnerAsync(CancellationToken cancellationToken) =>
        ReadAsync(cancellationToken);

    public async Task<ServiceResult<SiteSettingsDto>> UpdateAsync(
        SiteSettingsRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<SiteSettingsDto>.Failure(
                "validation", "Nama bisnis wajib diisi.");
        }

        if (!string.IsNullOrWhiteSpace(request.MapsUrl) &&
            (!Uri.TryCreate(request.MapsUrl.Trim(), UriKind.Absolute, out var mapsUri) ||
             (mapsUri.Scheme != Uri.UriSchemeHttp && mapsUri.Scheme != Uri.UriSchemeHttps)))
        {
            return ServiceResult<SiteSettingsDto>.Failure(
                "validation", "URL peta harus berupa alamat http atau https.");
        }

        var values = new Dictionary<string, string>
        {
            [NameKey] = name,
            [TaglineKey] = request.Tagline.Trim(),
            [DescriptionKey] = request.Description.Trim(),
            [AddressKey] = request.Address.Trim(),
            [PhoneKey] = request.Phone.Trim(),
            [InstagramKey] = request.Instagram.Trim(),
            [OpeningHoursKey] = request.OpeningHours.Trim(),
            [MapsUrlKey] = request.MapsUrl.Trim()
        };

        var existing = await dbContext.Settings
            .Where(x => BusinessKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        var now = timeProvider.GetUtcNow();

        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var setting))
            {
                setting.Value = JsonSerializer.Serialize(value);
                setting.UpdatedBy = actorId;
                setting.UpdatedAt = now;
            }
            else
            {
                dbContext.Settings.Add(new Setting
                {
                    Key = key,
                    Value = JsonSerializer.Serialize(value),
                    UpdatedBy = actorId,
                    UpdatedAt = now
                });
            }
        }

        await auditWriter.WriteAsync(
            actorId,
            "settings.updated",
            "settings",
            null,
            new { fields = values.Keys },
            ipAddress,
            cancellationToken);

        return ServiceResult<SiteSettingsDto>.Success(
            await ReadAsync(cancellationToken));
    }

    private async Task<SiteSettingsDto> ReadAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings
            .AsNoTracking()
            .Where(x => BusinessKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        return new SiteSettingsDto(
            ReadString(settings, NameKey, "HANORA CAFE"),
            ReadString(settings, TaglineKey, "A little pause, a lot of warmth."),
            ReadString(
                settings,
                DescriptionKey,
                "Tempat singgah untuk secangkir kopi yang dibuat dengan hati. " +
                "Nikmati rasa, ruang, dan cerita dalam setiap kunjungan."),
            ReadString(settings, AddressKey, string.Empty),
            ReadString(settings, PhoneKey, string.Empty),
            ReadString(settings, InstagramKey, string.Empty),
            ReadString(settings, OpeningHoursKey, string.Empty),
            ReadString(settings, MapsUrlKey, string.Empty),
            "/hanora-logo.jpeg");
    }

    private static string ReadString(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
