using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            if (sortBy is not (null or "" or "createdAtUtc" or "severity" or "status"))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sortBy"] = ["Допустимі значення: createdAtUtc, severity, status."]
                });

            var pattern = "%" + (q ?? "")
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_") + "%";

            var query = db.Incidents
                .AsNoTracking()
                .Where(x =>
                    EF.Functions.ILike(x.Title, pattern, "\\") ||
                    EF.Functions.ILike(x.Description, pattern, "\\"));

            query = sortBy switch
            {
                "severity" => query.OrderBy(x =>
                    x.Severity == IncidentSeverity.Critical ? 0 :
                    x.Severity == IncidentSeverity.High ? 1 :
                    x.Severity == IncidentSeverity.Medium ? 2 : 3),

                "status" => query.OrderBy(x =>
                    x.Status == IncidentStatus.New ? 0 :
                    x.Status == IncidentStatus.Triaged ? 1 :
                    x.Status == IncidentStatus.InProgress ? 2 :
                    x.Status == IncidentStatus.Resolved ? 3 : 4),

                _ => query.OrderByDescending(x => x.CreatedAtUtc)
            };

            var rows = await query.Take(50).ToListAsync(ct);

            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(),
                Status = row.Status.ToString(),
                row.CreatedAtUtc
            }));
        });

        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            var title = request.Title?.Trim();
            var description = request.Description?.Trim();

            if (string.IsNullOrEmpty(title))
                errors["title"] = ["Поле title є обов'язковим."];
            else if (title.Length > 160)
                errors["title"] = ["Максимальна довжина title — 160 символів."];

            if (string.IsNullOrEmpty(description))
                errors["description"] = ["Поле description є обов'язковим."];
            else if (description.Length > 4000)
                errors["description"] = ["Максимальна довжина description — 4000 символів."];

            var severityValid =
                Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity)
                && Enum.IsDefined(severity);

            if (!severityValid)
                errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];

            if (request.OccurredAtUtc is null)
                errors["occurredAtUtc"] = ["Поле occurredAtUtc є обов'язковим."];
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
                errors["occurredAtUtc"] = ["Дата не може бути більш ніж на 5 хвилин у майбутньому."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            if ((severity == IncidentSeverity.High || severity == IncidentSeverity.Critical)
                && description!.Length < 40)
            {
                errors["description"] =
                    ["Для High або Critical description має містити щонайменше 40 символів."];

                return Results.ValidationProblem(errors);
            }

            var duplicate = await db.Incidents.AnyAsync(
                x => x.Title == title && x.Status != IncidentStatus.Closed,
                ct);

            if (duplicate)
            {
                return Results.Problem(
                    title: "Конфлікт створення інциденту",
                    detail: "Активний інцидент із таким title вже існує.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = DbSeeder.AliceId,
                Title = title!,
                Description = description!,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.UpdatedAtUtc);

            return Results.Created($"/api/incidents/{incident.Id}", response);
        });
    }
}

public sealed record CreateIncidentRequest(
    string? Title,
    string? Description,
    string? Severity,
    DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);