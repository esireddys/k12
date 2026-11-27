using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Application.Practice.CheckAnswer;
using K12.Application.Practice.Sets;

namespace K12.Api.Endpoints;

/// <summary>
/// HTTP for the Practice context: open to guests and signed-in families alike, and only ever Live questions.
/// Endpoints only translate HTTP to use cases and back.
/// </summary>
public static class PracticeEndpoints
{
    public sealed record CheckAnswerBody(string? Response);

    public sealed record ScoreBody(IReadOnlyList<ResponseItem> Responses);

    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/practice").WithTags("Practice");

        group.MapGet("/catalog", async (IQueryHandler<GetPracticeCatalogQuery, PracticeCatalog> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetPracticeCatalogQuery(), ct)).ToHttp())
            .WithName("GetPracticeCatalog")
            .Produces<PracticeCatalog>();

        group.MapGet("/set", async (
                Guid courseId, SetSource source, Guid? formId, int? category, string? skill, int? count, int? seed,
                IQueryHandler<GetPracticeSetQuery, PracticeSet> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetPracticeSetQuery(courseId, source, formId, category, skill, count, seed), ct)).ToHttp())
            .WithName("GetPracticeSet")
            .Produces<PracticeSet>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/score", async (
                ScoreBody body, ICommandHandler<ScoreResponsesCommand, ScoreResult> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new ScoreResponsesCommand(body.Responses ?? []), ct)).ToHttp())
            .WithName("ScoreResponses")
            .Produces<ScoreResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/questions/{questionId:guid}/image", async (
                Guid questionId, IQueryHandler<GetQuestionImageQuery, ItemImage> handler, HttpContext http, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new GetQuestionImageQuery(questionId), ct);
                if (!result.IsSuccess) return result.ToHttp();
                http.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Stream(result.Value!.Content, result.Value.ContentType);
            })
            .WithName("GetQuestionImage")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/questions/{questionId:guid}/check", async (
                Guid questionId,
                CheckAnswerBody body,
                ICommandHandler<CheckAnswerCommand, CheckAnswerResponse> handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new CheckAnswerCommand(questionId, body.Response), ct)).ToHttp())
            .WithName("CheckAnswer")
            .Produces<CheckAnswerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
