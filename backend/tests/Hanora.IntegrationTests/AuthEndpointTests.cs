using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Hanora.IntegrationTests;

public sealed class AuthEndpointTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    [Fact]
    public async Task Login_Owner_MengembalikanTokenDanProfil()
    {
        using var client = factory.CreateClient();

        var login = await LoginAsync(
            client, AuthWebApplicationFactory.OwnerEmail, AuthWebApplicationFactory.OwnerPassword);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(900, login.ExpiresIn);
        Assert.Equal("Owner", login.User.Role);
        Assert.False(login.User.MustChangePassword);
    }

    [Fact]
    public async Task Login_PasswordSalah_Mengembalikan401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthWebApplicationFactory.OwnerEmail,
            password = "PasswordYangSalah123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerUsers_TanpaToken_Mengembalikan401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/owner/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerUsers_DenganKasir_Mengembalikan403()
    {
        using var client = factory.CreateClient();
        var login = await LoginAsync(
            client, AuthWebApplicationFactory.CashierEmail, AuthWebApplicationFactory.CashierPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await client.GetAsync("/api/v1/owner/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OwnerUsers_DenganOwner_Mengembalikan200()
    {
        using var client = factory.CreateClient();
        var login = await LoginAsync(
            client, AuthWebApplicationFactory.OwnerEmail, AuthWebApplicationFactory.OwnerPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await client.GetAsync("/api/v1/owner/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PagedUsers>();
        Assert.NotNull(payload);
        Assert.True(payload.TotalItems >= 3);
    }

    [Fact]
    public async Task WajibGantiPassword_MemblokirDashboard_LaluBisaDibukaSetelahGanti()
    {
        using var client = factory.CreateClient();
        var login = await LoginAsync(
            client, AuthWebApplicationFactory.ForcedEmail, AuthWebApplicationFactory.ForcedPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var blocked = await client.GetAsync("/api/v1/owner/users");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var changeResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = AuthWebApplicationFactory.ForcedPassword,
            newPassword = AuthWebApplicationFactory.ForcedNewPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        using var freshClient = factory.CreateClient();
        var oldLogin = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthWebApplicationFactory.ForcedEmail,
            password = AuthWebApplicationFactory.ForcedPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await LoginAsync(
            freshClient,
            AuthWebApplicationFactory.ForcedEmail,
            AuthWebApplicationFactory.ForcedNewPassword);
        freshClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", newLogin.AccessToken);

        var allowed = await freshClient.GetAsync("/api/v1/owner/users");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Refresh_Dan_Logout_MengelolaCookieDenganBenar()
    {
        using var client = factory.CreateClient();
        await LoginAsync(
            client, AuthWebApplicationFactory.OwnerEmail, AuthWebApplicationFactory.OwnerPassword);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refreshAfterLogout = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    private static async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private sealed record LoginResponse(
        string AccessToken,
        int ExpiresIn,
        UserPayload User);

    private sealed record UserPayload(
        Guid Id,
        string Name,
        string Email,
        string Role,
        bool MustChangePassword);

    private sealed record PagedUsers(
        IReadOnlyList<UserPayload> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);
}
