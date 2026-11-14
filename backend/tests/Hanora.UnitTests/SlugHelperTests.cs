using Hanora.Application.Common;

namespace Hanora.UnitTests;

public sealed class SlugHelperTests
{
    [Theory]
    [InlineData("Hanora Signature Latte", "hanora-signature-latte")]
    [InlineData("  Kopi Susu  ", "kopi-susu")]
    [InlineData("Matcha & Chocolate", "matcha-chocolate")]
    [InlineData("Café Spécial", "cafe-special")]
    [InlineData("", "item")]
    public void Slugify_MenghasilkanSlugYangDiharapkan(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Fact]
    public void Slugify_MenghilangkanKarakterBerulang()
    {
        Assert.Equal("kopi-susu", SlugHelper.Slugify("Kopi --- Susu!!!"));
    }
}
