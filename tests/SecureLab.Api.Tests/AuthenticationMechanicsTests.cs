
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/auth/session-check");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Register_Duplicate_Returns409()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = factory.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..8];
        var request = new
        {
            userName = $"reg{id}", email = $"reg{id}@example.test",
            displayName = "Test User", password
        };

        using var first = await client.PostAsJsonAsync("/api/auth/register", request);
        using var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnSameResponse()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = factory.CreateClient();

        using var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "test" + Guid.NewGuid().ToString("N"), password });
        using var wrong = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "alice", password = password + "_invalid" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Equal(unknown.Content.Headers.ContentType?.ToString(),
            wrong.Content.Headers.ContentType?.ToString());

        var a = await unknown.Content.ReadAsStringAsync();
        var b = await wrong.Content.ReadAsStringAsync();
        Assert.Equal(string.IsNullOrWhiteSpace(a), string.IsNullOrWhiteSpace(b));

        if (string.IsNullOrWhiteSpace(a)) return;

        using var first = JsonDocument.Parse(a);
        using var second = JsonDocument.Parse(b);

        foreach (var field in new[] { "type", "title", "status", "detail" })
        {
            var hasA = first.RootElement.TryGetProperty(field, out var x);
            var hasB = second.RootElement.TryGetProperty(field, out var y);
            Assert.Equal(hasA, hasB);
            if (hasA) Assert.Equal(x.GetRawText(), y.GetRawText());
        }
    }

    [Fact]
    public async Task A01_AnonymousMe_Returns401()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A02_LoginMe_ReturnsAlice()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var profile = json.RootElement;

        Assert.Equal("alice", profile.GetProperty("userName").GetString());
        Assert.Equal("10000000-0000-0000-0000-000000000001",
            profile.GetProperty("id").GetString());
        Assert.Equal(3, profile.EnumerateObject().Count());
    }

    [Fact]
    public async Task A03_ForgedHeader_DoesNotChangeUser()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);
        client.DefaultRequestHeaders.Add("X-Demo-UserId",
            "10000000-0000-0000-0000-000000000002");

        using var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("alice", json.RootElement.GetProperty("userName").GetString());
        Assert.Equal("10000000-0000-0000-0000-000000000001",
            json.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A06_Logout_Returns401()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var before = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var after = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Login_Cookie_HasSecureFlags()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "alice", password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookies = response.Headers.GetValues("Set-Cookie")
            .Where(x => x.StartsWith(".AspNetCore.Identity.Application="))
            .ToArray();

        Assert.True(cookies.Length == 1);

        var attributes = cookies[0].Split(';').Skip(1)
            .Select(x => x.Trim().ToLowerInvariant()).ToArray();

        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
    }

    [Fact]
    public async Task Login_RateLimit_RejectsAndRecovers()
    {
        using var host = factory.WithWebHostBuilder(_ => { });
        using var client = host.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { userName = "" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var blocked = await client.PostAsJsonAsync(
            "/api/auth/login", new { userName = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(6));

        using var recovered = await client.PostAsJsonAsync(
            "/api/auth/login", new { userName = "" });
        Assert.Equal(HttpStatusCode.Unauthorized, recovered.StatusCode);
    }
}
