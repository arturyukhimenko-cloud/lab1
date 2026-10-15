
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using System.Diagnostics;
using System.Security.Claims;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateIncidentAsync)
            .WithName("UpdateIncident")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteIncidentAsync)
            .WithName("DeleteIncident")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/triage", TriageIncidentAsync)
            .WithName("TriageIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>();

        endpoints.MapGet("/api/incident-comments/{id:guid}", GetIncidentCommentAsync)
            .WithName("GetIncidentComment")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private enum IncidentAction
    {
        Read,
        Update,
        Delete,
        ReadComment,
        Triage
    }

    private static bool CanAccessIncident(
        ClaimsPrincipal user,
        Guid userId,
        Guid ownerId,
        IncidentAction action,
        bool isInternal = false)
    {
        var isReporterOwner =
            user.IsInRole("Reporter") && ownerId == userId;

        var isAnalyst =
            user.IsInRole(DbSeeder.AnalystRole);

        return action switch
        {
            IncidentAction.Read =>
                isReporterOwner || isAnalyst,

            IncidentAction.Update =>
                isReporterOwner,

            IncidentAction.Delete =>
                false,

            IncidentAction.ReadComment =>
                isAnalyst || (isReporterOwner && !isInternal),

            IncidentAction.Triage =>
                isAnalyst,

            _ => false
        };
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var isAnalyst = httpContext.User.IsInRole(DbSeeder.AnalystRole);

        return Results.Ok(await queries.GetListAsync(
            parsedStatus, userId, isAnalyst, cancellationToken));
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var ownerId = await db.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => (Guid?)incident.OwnerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (ownerId is null)
            return Results.Problem(
                title: "Інцидент не знайдено",
                detail: $"Інцидент '{id}' не існує.",
                statusCode: StatusCodes.Status404NotFound);

        if (!CanAccessIncident(
            httpContext.User,
            userId,
            ownerId.Value,
            IncidentAction.Read))
            return Results.Forbid();

        var incident = await queries.GetDetailsAsync(
            id,
            cancellationToken,
            httpContext.User.IsInRole(DbSeeder.AnalystRole));

        return incident is null
            ? Results.NotFound()
            : Results.Ok(incident);
    }

    private static async Task<IResult> UpdateIncidentAsync(
        Guid id,
        UpdateIncidentRequest input,
        HttpContext httpContext,
        SecureLabDbContext db,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var incident = await db.Set<Incident>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incident is null)
            return Results.NotFound();

        if (!CanAccessIncident(
            httpContext.User,
            userId,
            incident.OwnerUserId,
            IncidentAction.Update))
            return Results.Forbid();

        incident.Title = input.Title;
        incident.Description = input.Description;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteIncidentAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var ownerId = await db.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => (Guid?)incident.OwnerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (ownerId is null)
            return Results.NotFound();

        if (!CanAccessIncident(
            httpContext.User,
            userId,
            ownerId.Value,
            IncidentAction.Delete))
            return Results.Forbid();

        return Results.Forbid();
    }

    private static async Task<IResult> GetIncidentCommentAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var comment = await db.Set<IncidentComment>()
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.IncidentId,
                x.Incident.OwnerUserId,
                AuthorDisplayName = x.Author.DisplayName,
                x.Text,
                x.IsInternal,
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (comment is null)
            return Results.NotFound();

        if (!CanAccessIncident(
            httpContext.User,
            userId,
            comment.OwnerUserId,
            IncidentAction.ReadComment,
            comment.IsInternal))
            return Results.Forbid();

        return Results.Ok(new
        {
            comment.Id,
            comment.IncidentId,
            comment.AuthorDisplayName,
            comment.Text,
            comment.IsInternal,
            comment.CreatedAtUtc
        });
    }

    private static async Task<IResult> TriageIncidentAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Challenge();

        var incident = await db.Set<Incident>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incident is null)
            return Results.NotFound();

        if (!CanAccessIncident(
            httpContext.User,
            userId,
            incident.OwnerUserId,
            IncidentAction.Triage)
            || incident.Status != IncidentStatus.New)
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            loggerFactory.CreateLogger("SecureLab.Authorization").LogWarning(
                "authorization.denied subject={SubjectId} action={Action} resource={ResourceId} result={Result} occurredAtUtc={OccurredAtUtc:O} traceId={TraceId}",
                userId,
                "incident.triage",
                id,
                "denied",
                DateTimeOffset.UtcNow,
                traceId);

            return Results.Forbid();
        }

        var now = DateTimeOffset.UtcNow;

        db.Set<IncidentStatusHistory>().Add(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ChangedByUserId = userId,
            OldStatus = incident.Status,
            NewStatus = IncidentStatus.Triaged,
            Note = "Перехід New → Triaged",
            CreatedAtUtc = now
        });

        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await queries.GetSeveritySummaryAsync(cancellationToken));
    }
}

public sealed record UpdateIncidentRequest(string Title, string Description);
