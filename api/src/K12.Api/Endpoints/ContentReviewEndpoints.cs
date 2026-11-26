using System.Security.Claims;
using System.Text.Json;
using K12.Api.Auth;
using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;

namespace K12.Api.Endpoints;

/// <summary>
/// HTTP for content review: the AI reviewer job reads its queue and records findings;
/// owners work through their queue and approve or reject. Nothing here is open to parents or students.
/// </summary>
public static class ContentReviewEndpoints
{
    public sealed record RecordAiReviewBody(
        int Version, string Reviewer, AiVerdict Verdict, bool AnswerKeyAgrees, string SolvedAnswer,
        Difficulty SuggestedDifficulty, JsonElement ProposedContent, string Summary, IReadOnlyList<AiFinding> Findings);

    /// <summary>Leave Content, Answer and Difficulty out to approve the question exactly as it stands.</summary>
    public sealed record ApproveBody(int Version, JsonElement? Content, AnswerSpec? Answer, Difficulty? Difficulty, string? Note);

    public sealed record RejectBody(int Version, string Reason);

    public sealed record MeResponse(string Name, bool IsOwner);

    public static IEndpointRouteBuilder MapContentReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
                user.Identity?.IsAuthenticated == true ? user.ReviewerName() : "",
                user.IsInRole(Access.OwnerRole))))
            .WithTags("Account")
            .WithName("GetMe")
            .Produces<MeResponse>();

        var agent = app.MapGroup("/api/admin/review").WithTags("AI review").RequireAuthorization(Access.ReviewAgentPolicy);

        agent.MapGet("/ai-queue", async (
                int? take, IQueryHandler<ListAiReviewQueueQuery, AiReviewQueueResponse> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new ListAiReviewQueueQuery(take ?? 50), ct)).ToHttp())
            .WithName("ListAiReviewQueue")
            .Produces<AiReviewQueueResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        agent.MapPost("/questions/{questionId:guid}/ai-review", async (
                Guid questionId, RecordAiReviewBody body,
                ICommandHandler<RecordAiReviewCommand, ReviewStepResponse> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new RecordAiReviewCommand(
                questionId, body.Version, body.Reviewer, body.Verdict, body.AnswerKeyAgrees, body.SolvedAnswer,
                body.SuggestedDifficulty, body.ProposedContent, body.Summary, body.Findings), ct)).ToHttp())
            .WithName("RecordAiReview")
            .Produces<ReviewStepResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var owner = app.MapGroup("/api/admin/review").WithTags("Owner review").RequireAuthorization(Access.OwnerPolicy);

        owner.MapGet("/queue", async (IQueryHandler<GetOwnerQueueQuery, OwnerQueueResponse> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetOwnerQueueQuery(), ct)).ToHttp())
            .WithName("GetOwnerQueue")
            .Produces<OwnerQueueResponse>();

        owner.MapGet("/questions/{questionId:guid}", async (
                Guid questionId, IQueryHandler<GetQuestionForReviewQuery, QuestionForReview> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetQuestionForReviewQuery(questionId), ct)).ToHttp())
            .WithName("GetQuestionForReview")
            .Produces<QuestionForReview>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        owner.MapPost("/questions/{questionId:guid}/approve", async (
                Guid questionId, ApproveBody body, ClaimsPrincipal user,
                ICommandHandler<ApproveQuestionCommand, ReviewStepResponse> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new ApproveQuestionCommand(
                questionId, body.Version, user.ReviewerName(), body.Content, body.Answer, body.Difficulty, body.Note), ct)).ToHttp())
            .WithName("ApproveQuestion")
            .Produces<ReviewStepResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        owner.MapPost("/questions/{questionId:guid}/reject", async (
                Guid questionId, RejectBody body, ClaimsPrincipal user,
                ICommandHandler<RejectQuestionCommand, ReviewStepResponse> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new RejectQuestionCommand(questionId, body.Version, user.ReviewerName(), body.Reason), ct)).ToHttp())
            .WithName("RejectQuestion")
            .Produces<ReviewStepResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        owner.MapGet("/images/{**path}", async (
                string path, IQueryHandler<GetItemImageQuery, ItemImage> handler, HttpContext http, CancellationToken ct) =>
            {
                // Clients may send the folder separator encoded (form%2F01.png); the store still refuses anything outside its root.
                var result = await handler.HandleAsync(new GetItemImageQuery(Uri.UnescapeDataString(path)), ct);
                if (!result.IsSuccess) return result.ToHttp();
                http.Response.Headers.CacheControl = "private, max-age=3600";
                return Results.Stream(result.Value!.Content, result.Value.ContentType);
            })
            .WithName("GetItemImage")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
