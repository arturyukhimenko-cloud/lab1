
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public static class CookieClient
{
    public static async Task<HttpClient> LoginAsync(
        this SecureLabApiFactory factory,
        string userName,
        string password)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName, password });

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;

            var retryAfter =
                response.Headers.RetryAfter?.Delta?.ToString()
                ?? "not specified";

            client.Dispose();

            throw new InvalidOperationException(
                $"Study login failed for '{userName}'. " +
                $"HTTP {statusCode} ({response.StatusCode}). " +
                $"Retry-After: {retryAfter}. " +
                "Credentials omitted.");
        }

        return client;
    }
}
