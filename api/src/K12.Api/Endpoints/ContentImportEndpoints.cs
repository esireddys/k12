using System.Text.Json;
using K12.Application.Abstractions;
using K12.Application.ContentImport.ImportCourse;
using K12.Application.ContentImport.ImportExamForm;
using K12.Application.ContentImport.PublishReleased;
using K12.Domain.Content.Answers;

namespace K12.Api.Endpoints;

/// <summary>
/// HTTP for loading curriculum and released tests from the content pipeline.
/// Mapped in Development only until owner sign-in exists; then it moves behind the admin role.
/// </summary>
public static class ContentImportEndpoints
{
    public sealed record ImportSkillBody(string Code, string Description, int? Category, string? Designation, JsonElement? Limits);

    public sealed record ImportCourseBody(
        string Jurisdiction, string Subject, string Code, string Name,
        string FrameworkCode, string FrameworkName,
        IReadOnlyList<ImportCategory> Categories, IReadOnlyList<ImportSkillBody> Skills, string? JurisdictionName = null,
        string? Kind = null, int? TypicalGrade = null, string? Language = null, ImportPracticeTest? PracticeTest = null);

    public sealed record ImportItemBody(
        int Number, IReadOnlyList<string> SkillCodes, string ItemType, JsonElement Content, AnswerSpec Answer,
        string? Passage = null, int? Category = null);

    public sealed record ImportPassageBody(string Key, string Title, JsonElement Content);

    public sealed record ImportExamFormBody(
        string Jurisdiction, string Subject, string CourseCode,
        string Exam, int Year, string Season, string SourceUrl,
        IReadOnlyList<ImportItemBody> Items, IReadOnlyList<ImportPassageBody>? Passages = null, string? Language = null);

    public static IEndpointRouteBuilder MapContentImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/import").WithTags("Content import");

        group.MapPost("/course", async (
                ImportCourseBody body,
                ICommandHandler<ImportCourseCommand, ImportCourseResponse> handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new ImportCourseCommand(
                body.Jurisdiction, body.Subject, body.Code, body.Name, body.FrameworkCode, body.FrameworkName,
                body.Categories,
                body.Skills.Select(s => new ImportSkill(s.Code, s.Description, s.Category, s.Designation, s.Limits?.GetRawText())).ToList(),
                body.JurisdictionName, body.Kind, body.TypicalGrade, body.Language, body.PracticeTest),
                ct)).ToHttp())
            .WithName("ImportCourse")
            .Produces<ImportCourseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/exam-form", async (
                ImportExamFormBody body,
                ICommandHandler<ImportExamFormCommand, ImportExamFormResponse> handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new ImportExamFormCommand(
                body.Jurisdiction, body.Subject, body.CourseCode, body.Exam, body.Year, body.Season, body.SourceUrl,
                body.Items.Select(i => new ImportItem(
                    i.Number, i.SkillCodes, i.ItemType, i.Content.GetRawText(), i.Answer, i.Passage, i.Category)).ToList(),
                (body.Passages ?? []).Select(p => new ImportPassage(p.Key, p.Title, p.Content.GetRawText())).ToList(),
                body.Language),
                ct)).ToHttp())
            .WithName("ImportExamForm")
            .Produces<ImportExamFormResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/publish-released", async (
                ICommandHandler<PublishReleasedQuestionsCommand, PublishReleasedQuestionsResponse> handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new PublishReleasedQuestionsCommand(), ct)).ToHttp())
            .WithName("PublishReleasedQuestions")
            .Produces<PublishReleasedQuestionsResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
