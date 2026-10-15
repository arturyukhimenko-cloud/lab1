
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetList_ReturnsSeededIncidents()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("morgan", password);

        var incidents = await client.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        Assert.NotNull(incidents);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.AliceIncidentId);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync($"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDetails_DoesNotExposeInternalOwnerFields()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("ownerUserId", out _));
        Assert.False(document.RootElement.TryGetProperty("email", out _));
        Assert.Equal("Аліса Коваль",
            document.RootElement.GetProperty("ownerDisplayName").GetString());
    }

    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        var script = await _client.GetStringAsync("/app.js");

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A04_A05_OwnerIsBob(bool forgedOwner)
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("bob", password);

        var request = new Dictionary<string, object>
        {
            ["title"] = $"BobTest_{Guid.NewGuid():N}",
            ["description"] = "Навчальний інцидент",
            ["severity"] = "Low",
            ["occurredAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        if (forgedOwner)
            request["ownerUserId"] = DbSeeder.AliceId;

        using var response = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var incident = await db.Incidents.FindAsync(id);

        Assert.NotNull(incident);
        Assert.Equal(DbSeeder.BobId, incident.OwnerUserId);
    }

    [Fact]
    public async Task A02_Alice_CanReadOwnIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A03_Alice_CannotReadBobIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A06_Reporter_CannotTriageOwnIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        const string path =
            "/api/incidents/20000000-0000-0000-0000-000000000005";

        var before = await client.GetFromJsonAsync<IncidentDetailsResponse>(path);

        Assert.NotNull(before);
        Assert.Equal("New", before.Status);

        using var response = await client.PostAsync($"{path}/triage", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var after = await client.GetFromJsonAsync<IncidentDetailsResponse>(path);

        Assert.NotNull(after);
        Assert.Equal(before.Status, after.Status);
    }

    [Fact]
    public async Task A07_Analyst_CanTriageNewIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("morgan", password);

        var request = new
        {
            title = $"TriageTest_{Guid.NewGuid():N}",
            description = "Тест дозволеного переходу",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        using var created = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        var path = $"/api/incidents/{id}";

        try
        {
            var before = await client.GetFromJsonAsync<IncidentDetailsResponse>(path);
            Assert.NotNull(before);
            Assert.Equal("New", before.Status);

            using var response = await client.PostAsync($"{path}/triage", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var after = await client.GetFromJsonAsync<IncidentDetailsResponse>(path);
            Assert.NotNull(after);
            Assert.Equal("Triaged", after.Status);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            await db.Set<IncidentStatusHistory>()
                .Where(x => x.IncidentId == id)
                .ExecuteDeleteAsync();

            await db.Set<Incident>()
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task A01_Anonymous_CannotAccessProtectedIncidents()
    {
        using var readResponse = await _client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.Unauthorized, readResponse.StatusCode);
        Assert.Null(readResponse.Headers.Location);

        using var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Anonymous Test",
                description = "Unauthorized request",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Null(createResponse.Headers.Location);
    }

    [Fact]
    public async Task A04_Alice_CannotUpdateOrDeleteBobIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var alice = await factory.LoginAsync("alice", password);
        using var morgan = await factory.LoginAsync("morgan", password);

        var path = $"/api/incidents/{DbSeeder.BobIncidentId}";

        var before = await morgan.GetFromJsonAsync<IncidentDetailsResponse>(path);
        Assert.NotNull(before);

        using var updateResponse = await alice.PutAsJsonAsync(path, new
        {
            title = "Unauthorized Update",
            description = "Attempted change by Alice"
        });

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        using var deleteResponse = await alice.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var after = await morgan.GetFromJsonAsync<IncidentDetailsResponse>(path);
        Assert.NotNull(after);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Title, after.Title);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.Status, after.Status);
    }

    [Fact]
    public async Task A03R_Analyst_CanReadAliceIncident()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var morgan = await factory.LoginAsync("morgan", password);

        using var response = await morgan.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A05_Reporter_SeesOnlyOwnIncidents()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var alice = await factory.LoginAsync("alice", password);
        using var morgan = await factory.LoginAsync("morgan", password);

        var aliceList = await alice.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        var morganList = await morgan.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        Assert.NotNull(aliceList);
        Assert.NotNull(morganList);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

        var aliceIds = await db.Incidents
            .AsNoTracking()
            .Where(x => x.OwnerUserId == DbSeeder.AliceId)
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(
            aliceIds.OrderBy(x => x),
            aliceList.Select(x => x.Id).OrderBy(x => x));

        Assert.Contains(morganList, x => x.Id == DbSeeder.AliceIncidentId);
        Assert.Contains(morganList, x => x.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task A08a_Overposting_ProtectsFields()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var alice = await factory.LoginAsync("alice", password);

        using var created = await alice.PostAsJsonAsync("/api/incidents", new
        {
            title = $"Overposting_{Guid.NewGuid():N}",
            description = "Original description",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        var path = $"/api/incidents/{id}";

        try
        {
            using var updated = await alice.PutAsJsonAsync(path, new
            {
                title = "Allowed update",
                description = "Updated description",
                ownerUserId = DbSeeder.BobId,
                role = "Analyst",
                status = "Closed"
            });

            Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            var incident = await db.Incidents
                .AsNoTracking()
                .SingleAsync(x => x.Id == id);

            Assert.Equal(DbSeeder.AliceId, incident.OwnerUserId);
            Assert.Equal(IncidentStatus.New, incident.Status);
            Assert.Equal("Allowed update", incident.Title);
            Assert.Equal("Updated description", incident.Description);

            using var denied = await alice.PostAsync($"{path}/triage", null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            await db.Set<IncidentStatusHistory>()
                .Where(x => x.IncidentId == id)
                .ExecuteDeleteAsync();

            await db.Set<Incident>()
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task A12_ChildComments_Authorization()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        using var alice = await factory.LoginAsync("alice", password);
        using var morgan = await factory.LoginAsync("morgan", password);

        const string comments = "/api/incident-comments/";

        const string ownId = "30000000-0000-0000-0000-000000000001";
        const string bobId = "30000000-0000-0000-0000-000000000002";
        const string internalId = "30000000-0000-0000-0000-000000000003";

        using var own = await alice.GetAsync(comments + ownId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        using var foreign = await alice.GetAsync(comments + bobId);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        using var hidden = await alice.GetAsync(comments + internalId);
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);

        using var analyst = await morgan.GetAsync(comments + internalId);
        Assert.Equal(HttpStatusCode.OK, analyst.StatusCode);

        using (var json = JsonDocument.Parse(
            await analyst.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("isInternal").GetBoolean());
        }

        using var anonymous = await _client.GetAsync(comments + internalId);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var missing = await alice.GetAsync(comments + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var aliceDetails = await alice.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        using var aliceJson = JsonDocument.Parse(
            await aliceDetails.Content.ReadAsStringAsync());

        var aliceComments = aliceJson.RootElement
            .GetProperty("comments")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToArray();

        Assert.DoesNotContain(Guid.Parse(internalId), aliceComments);

        using var morganDetails = await morgan.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        using var morganJson = JsonDocument.Parse(
            await morganDetails.Content.ReadAsStringAsync());

        var morganComments = morganJson.RootElement
            .GetProperty("comments")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToArray();

        Assert.Contains(Guid.Parse(internalId), morganComments);
    }
}
