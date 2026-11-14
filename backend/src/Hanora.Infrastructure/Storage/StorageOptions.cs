namespace Hanora.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Path absolut atau relatif terhadap content root API.
    /// </summary>
    public string UploadPath { get; set; } = "uploads";
}
