using System.Net;
using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

public sealed class SearchMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Search_ReturnsJson()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/incidents/search?q=навчальн");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task T02_InvalidSeverity_Returns400()
    {
        using var client = factory.CreateClient();

        var request = new
        {
            title = $"T02-{Guid.NewGuid():N}",
            description = "Перевірка некоректного severity.",
            severity = "7",
            occurredAtUtc = DateTimeOffset.UtcNow
        };

        using var response = await client.PostAsJsonAsync("/api/incidents", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("severity", body);
    }

    [Fact]
    public async Task T03_DuplicateTitle_Returns409()
    {
        using var client = factory.CreateClient();

        var title = $"T03-{Guid.NewGuid():N}";

        var request = new
        {
            title,
            description = "Перевірка предметного конфлікту.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        };

        using var firstResponse = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync("/api/incidents", request);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }
}