using K12.Application.Abstractions;
using K12.Application.ContentImport.ImportExamForm;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.ContentImport.RefreshExamForm;

/// <summary>A released test imported again, after the pipeline learned to read it better. Same body as the import.</summary>
public sealed record RefreshExamFormCommand(ImportExamFormCommand Form);

/// <param name="QuestionsRefreshed">Questions whose content changed.</param>
/// <param name="QuestionsKeptAsEdited">Questions an owner edited, left as they are.</param>
public sealed record RefreshExamFormResponse(Guid ExamFormId, int QuestionsRefreshed, int QuestionsKeptAsEdited);

/// <summary>
/// Updates the questions of an imported form with a newer reading of the same PDF: the pictures and which text
/// students see. Items keep their ids, status, history and kids' progress. Questions an owner edited are kept.
/// Safe to run again: a second run with the same body changes nothing.
/// </summary>
public sealed class RefreshExamFormHandler(
    ICourseRepository courses, IExamFormRepository forms, IQuestionRepository questions, IUnitOfWork unitOfWork)
    : ICommandHandler<RefreshExamFormCommand, RefreshExamFormResponse>
{
    public async Task<Result<RefreshExamFormResponse>> HandleAsync(RefreshExamFormCommand command, CancellationToken ct = default)
    {
        var body = command.Form;
        var course = await courses.FindAsync(body.Jurisdiction, body.Subject, body.CourseCode, body.Language ?? Course.DefaultLanguage, ct);
        if (course is null)
            return Error.NotFound("import.course_not_found", $"Import the {body.Jurisdiction} {body.Subject} {body.CourseCode} curriculum first.");
        var form = await forms.FindAsync(course.Id, body.Exam, body.Year, body.Season, ct);
        if (form is null)
            return Error.NotFound("import.form_not_found", $"{body.Exam} {body.Season} {body.Year} is not imported yet. Import it first.");

        var numbers = form.Items.Select(i => i.Number).Order().ToList();
        if (!numbers.SequenceEqual(body.Items.Select(i => i.Number).Order()))
            return Error.Validation("import.items_differ", "The import has different item numbers from the form. Refreshing cannot add or remove items.");

        var byNumber = form.Items.ToDictionary(i => i.Number, i => i.QuestionId);
        var loaded = (await questions.ListByIdsAsync(byNumber.Values.ToList(), ct)).ToDictionary(q => q.Id);
        var refreshed = 0;
        var kept = 0;
        try
        {
            foreach (var item in body.Items)
            {
                if (!loaded.TryGetValue(byNumber[item.Number], out var question)) continue;
                if (question.Version != 1)
                {
                    kept++;
                    continue;
                }
                if (question.RefreshReleasedContent(item.ContentJson, item.Answer)) refreshed++;
            }
        }
        catch (DomainException e)
        {
            return Error.Validation("import.invalid_item", e.Message);
        }

        if (refreshed > 0)
        {
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (ConcurrencyConflictException)
            {
                return Error.Conflict("import.refresh_conflict", "Some questions changed while refreshing. Run it again.");
            }
        }
        return new RefreshExamFormResponse(form.Id.Value, refreshed, kept);
    }
}
