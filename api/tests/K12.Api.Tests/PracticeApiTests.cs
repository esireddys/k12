using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace K12.Api.Tests;

/// <summary>A guest picks a course and practices: catalog, a year's test, scoring with the key, pictures.</summary>
public sealed class PracticeApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<string> ImportAsync()
    {
        var admin = _factory.CreateClient();
        (await admin.PostAsJsonAsync("/api/admin/import/course", new
        {
            jurisdiction = "ZZ", jurisdictionName = "Zedland", subject = "math", code = "G3", name = "Grade 3",
            frameworkCode = "FW", frameworkName = "Framework",
            categories = new[] { new { number = 1, name = "Numbers" } },
            skills = new[] { new { code = "3.4(K)", description = "Multiply", category = 1, designation = "readiness" } },
        })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/admin/import/exam-form", new
        {
            jurisdiction = "ZZ", subject = "math", courseCode = "G3", exam = "EXAM", year = 2022, season = "spring",
            sourceUrl = "https://example.org/t.pdf",
            items = new object[]
            {
                new
                {
                    number = 1, skillCodes = new[] { "3.4(K)" }, itemType = "choice",
                    content = new { image = "form/01.png", stem = "6 x 4 = ?", choices = new[] { new { label = "A", text = "10" }, new { label = "B", text = "24" } } },
                    answer = new { kind = "Choice", value = "B" },
                },
                new
                {
                    number = 2, skillCodes = new[] { "3.4(K)" }, itemType = "numeric",
                    content = new { image = "form/02.png", stem = "7 x 3 = ?", textNeedsReview = true },
                    answer = new { kind = "Integer", value = "21" },
                },
            },
        })).EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.Combine(_factory.ImageRoot, "form"));
        await File.WriteAllBytesAsync(Path.Combine(_factory.ImageRoot, "form", "01.png"), [137, 80, 78, 71]);

        var catalog = await _factory.CreateClient().GetFromJsonAsync<JsonObject>("/api/practice/catalog");
        var course = catalog!["courses"]!.AsArray().Single(c => (string)c!["jurisdiction"]! == "ZZ")!;
        Assert.Equal(("Zedland", 2), ((string)course["jurisdictionName"]!, (int)course["questionCount"]!));
        return (string)course["id"]!;
    }

    [Fact]
    public async Task A_student_takes_a_years_test_and_gets_the_key_at_the_end()
    {
        var courseId = await ImportAsync();
        var client = _factory.CreateClient();

        var catalog = await client.GetFromJsonAsync<JsonObject>("/api/practice/catalog");
        var formId = (string)catalog!["courses"]!.AsArray().Single(c => (string)c!["id"]! == courseId)!["forms"]![0]!["id"]!;

        var set = await client.GetFromJsonAsync<JsonObject>($"/api/practice/set?courseId={courseId}&source=Year&formId={formId}");
        var questions = set!["questions"]!.AsArray();
        Assert.Equal(["Choice", "Integer"], questions.Select(q => (string)q!["answerKind"]!));
        Assert.DoesNotContain("\"21\"", set.ToJsonString());

        var score = await client.PostAsJsonAsync("/api/practice/score", new
        {
            responses = new[]
            {
                new { questionId = (string)questions[0]!["id"]!, response = "B" },
                new { questionId = (string)questions[1]!["id"]!, response = "20" },
            },
        });
        var result = (await score.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((1, 2), ((int)result["correct"]!, (int)result["total"]!));
        Assert.Equal(("B", "24"), ((string)result["items"]![0]!["correctAnswer"]!, (string)result["items"]![0]!["correctChoiceText"]!));
        Assert.Equal("21", (string)result["items"]![1]!["correctAnswer"]!);
    }

    [Fact]
    public async Task Students_get_a_live_questions_picture_by_question_id()
    {
        var courseId = await ImportAsync();
        var client = _factory.CreateClient();
        var set = await client.GetFromJsonAsync<JsonObject>($"/api/practice/set?courseId={courseId}&source=Shuffle&count=10&seed=1");
        var ids = set!["questions"]!.AsArray().ToDictionary(q => (string)q!["stem"]!, q => (string)q!["id"]!);

        var ok = await client.GetAsync($"/api/practice/questions/{ids["6 x 4 = ?"]}/image");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("image/png", ok.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/practice/questions/{ids["7 x 3 = ?"]}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/practice/questions/{Guid.NewGuid()}/image")).StatusCode);
    }

    [Fact]
    public async Task A_set_with_nothing_to_build_from_is_a_bad_request()
    {
        var courseId = await ImportAsync();
        var response = await _factory.CreateClient().GetAsync($"/api/practice/set?courseId={courseId}&source=Topic");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anyone_can_report_a_problem_and_a_guest_report_is_marked_as_a_guests()
    {
        var courseId = await ImportAsync();
        var client = _factory.CreateClient();
        var set = await client.GetFromJsonAsync<JsonObject>($"/api/practice/set?courseId={courseId}&source=Today&count=5");
        Assert.Equal("Today's practice", (string)set!["title"]!);
        var questionId = (string)set["questions"]![0]!["id"]!;

        // Not signed in as a family, the caller claims to be a parent: it is still recorded as a guest.
        var sent = await client.PostAsJsonAsync($"/api/practice/questions/{questionId}/reports", new { reason = "HardToRead", reporter = "Parent" });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var reports = await client.GetFromJsonAsync<JsonObject>("/api/admin/review/reports");
        var report = Assert.Single(reports!["reports"]!.AsArray())!;
        Assert.Equal(("HardToRead", "Guest", questionId), ((string)report["reason"]!, (string)report["reporter"]!, (string)report["questionId"]!));

        var bad = await client.PostAsJsonAsync($"/api/practice/questions/{Guid.NewGuid()}/reports", new { reason = "Typo" });
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
    }
}
