using System.Net;
using System.Net.Http.Json;

namespace Hanora.IntegrationTests;

public sealed class RateLimitTests(RateLimitWebApplicationFactory factory)
    : IClassFixture<RateLimitWebApplicationFactory>
{
    [Fact]
    public async Task Login_TerkenaRateLimitSetelahBatas()
    {
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new
                {
                    email = "tidak-ada@test.local",
                    password = "PasswordSalah123!"
                });

            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.True(response.Headers.Contains("Retry-After"));
            }
        }

        Assert.Equal(3, statuses.Count(x => x == HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[3]);
    }
}
