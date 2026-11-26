using K12.Application.Abstractions;
using K12.Application.Practice.CheckAnswer;

namespace K12.Api.Endpoints;

/// <summary>HTTP for the Practice context. Endpoints only translate HTTP to use cases and back.</summary>
public static class PracticeEndpoints
{
    public sealed record CheckAnswerBody(string? Response);

    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/practice").WithTags("Practice");

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
