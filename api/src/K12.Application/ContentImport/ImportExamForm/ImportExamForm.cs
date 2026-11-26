using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.ContentImport.ImportExamForm;

/// <param name="Number">The item number on the printed test.</param>
/// <param name="SkillCodes">Skill codes from the course, for example "3.4(K)".</param>
/// <param name="ItemType">How the item is answered, for example "choice" or "numeric".</param>
/// <param name="ContentJson">Stem, choices and figure references, as JSON.</param>
public sealed record ImportItem(int Number, IReadOnlyList<string> SkillCodes, string ItemType, string ContentJson, AnswerSpec Answer);

/// <summary>A released test as the content pipeline publishes it.</summary>
public sealed record ImportExamFormCommand(
    string Jurisdiction, string Subject, string CourseCode,
    string Exam, int Year, string Season, string SourceUrl,
    IReadOnlyList<ImportItem> Items);

public sealed record ImportExamFormResponse(Guid ExamFormId, int QuestionsCreated);

/// <summary>
/// Turns a released test into Official questions and an exam form that keeps their order.
/// New questions go straight to AI review; nothing reaches students until an owner approves it.
/// A form is imported once: importing the same exam, year and season again is a conflict.
/// </summary>
public sealed class ImportExamFormHandler(
    ICourseRepository courses, ISkillRepository skills, IQuestionRepository questions,
    IExamFormRepository forms, IUnitOfWork unitOfWork)
    : ICommandHandler<ImportExamFormCommand, ImportExamFormResponse>
{
    public async Task<Result<ImportExamFormResponse>> HandleAsync(ImportExamFormCommand command, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(command.Jurisdiction, command.Subject, command.CourseCode, ct);
        if (course is null)
            return Error.NotFound("import.course_not_found",
                $"Import the {command.Jurisdiction} {command.Subject} {command.CourseCode} curriculum first.");

        if (command.Items.Count == 0)
            return Error.Validation("import.no_items", "The form has no items.");

        var repeated = command.Items.GroupBy(i => i.Number).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            return Error.Validation("import.duplicate_item", $"Item {repeated.Key} appears more than once.");

        var existing = await forms.FindAsync(course.Id, command.Exam, command.Year, command.Season, ct);
        if (existing is not null)
            return Error.Conflict("import.form_exists",
                $"{command.Exam} {command.Season} {command.Year} is already imported as form {existing.Id.Value}.");

        var skillsByCode = (await skills.ListByCourseAsync(course.Id, ct))
            .ToDictionary(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var unknown = command.Items.SelectMany(i => i.SkillCodes).Where(c => !skillsByCode.ContainsKey(c.Trim())).Distinct().ToList();
        if (unknown.Count > 0)
            return Error.Validation("import.unknown_skill", $"These skills are not in the course: {string.Join(", ", unknown)}.");

        try
        {
            var form = ExamForm.Create(course.Id, command.Exam, command.Year, command.Season, command.SourceUrl);
            foreach (var item in command.Items.OrderBy(i => i.Number))
            {
                var question = Question.Draft(
                    item.SkillCodes.Select(c => skillsByCode[c.Trim()]),
                    item.ItemType, item.ContentJson, item.Answer, Difficulty.OnLevel, QuestionOrigin.Official);
                question.SubmitForAiReview();
                form.AddItem(item.Number, question.Id);
                await questions.AddAsync(question, ct);
            }

            await forms.AddAsync(form, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return new ImportExamFormResponse(form.Id.Value, command.Items.Count);
        }
        catch (DomainException e)
        {
            return Error.Validation("import.invalid_item", e.Message);
        }
    }
}
