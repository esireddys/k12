using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using K12.Domain.Progress;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Api.Tests;

/// <summary>A signed-in kid's answers are saved; a guest's are not; the parent dashboard sits behind the parent PIN.</summary>
public sealed class ProgressApiTests : IDisposable
{
    private readonly ApiFactory _factory = new() { DevelopmentUser = "" };

    public void Dispose() => _factory.Dispose();

    private static async Task<JsonObject> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    /// <summary>A course with two Live questions; returns the course and question ids.</summary>
    private async Task<(string CourseId, string Q1, string Q2)> ImportAsync()
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
                new { number = 1, skillCodes = new[] { "3.4(K)" }, itemType = "numeric", content = new { stem = "6 x 4 = ?" }, answer = new { kind = "Integer", value = "24" } },
                new { number = 2, skillCodes = new[] { "3.4(K)" }, itemType = "numeric", content = new { stem = "7 x 3 = ?" }, answer = new { kind = "Integer", value = "21" } },
            },
        })).EnsureSuccessStatusCode();

        var catalog = await admin.GetFromJsonAsync<JsonObject>("/api/practice/catalog");
        var course = catalog!["courses"]!.AsArray().Single(c => (string)c!["jurisdiction"]! == "ZZ")!;
        var form = (string)course["forms"]!.AsArray()[0]!["id"]!;
        var set = await admin.GetFromJsonAsync<JsonObject>($"/api/practice/set?courseId={course["id"]}&source=Year&formId={form}");
        var questions = set!["questions"]!.AsArray();
        return ((string)course["id"]!, (string)questions[0]!["id"]!, (string)questions[1]!["id"]!);
    }

    private async Task<(HttpClient Parent, string KidId)> FamilyAsync(string courseId)
    {
        var parent = _factory.CreateClient();
        await Json(await parent.PostAsJsonAsync("/api/auth/developer", new { email = "pat@example.org", name = "Pat" }));
        var kid = await Json(await parent.PostAsJsonAsync("/api/family/kids", new { name = "Maya", avatar = "Orange", courseId }));
        return (parent, (string)kid["id"]!);
    }

    [Fact]
    public async Task A_guest_practices_and_nothing_is_saved()
    {
        var (_, q1, _) = await ImportAsync();
        var guest = _factory.CreateClient();
        await Json(await guest.PostAsJsonAsync("/api/practice/score", new { responses = new[] { new { questionId = q1, response = "5" } } }));

        var record = await guest.PostAsJsonAsync($"/api/family/kids/{Guid.NewGuid()}/answers", new
        {
            sessionId = Guid.NewGuid(), mode = "Practice", responses = new[] { new { questionId = q1, response = "5" } },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, record.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"/api/family/kids/{Guid.NewGuid()}/summary")).StatusCode);

        var sessions = _factory.Services.GetRequiredService<IPracticeSessionRepository>();
        Assert.Null(await sessions.GetAsync(new PracticeSessionId(Guid.Empty)));
        var progress = _factory.Services.GetRequiredService<IKidProgressRepository>();
        Assert.Empty(await progress.ListAsync([]));
    }

    [Fact]
    public async Task A_kids_answers_build_their_streak_stars_and_mistakes()
    {
        var (courseId, q1, q2) = await ImportAsync();
        var (parent, kidId) = await FamilyAsync(courseId);
        var session = Guid.NewGuid();

        var first = await Json(await parent.PostAsJsonAsync($"/api/family/kids/{kidId}/answers", new
        {
            sessionId = session, mode = "Practice", source = "Topic", title = "Multiply", localDate = Today, elapsedSeconds = 40,
            responses = new[] { new { questionId = q1, response = "24" } },
        }));
        Assert.True((bool)first["score"]!["items"]![0]!["isCorrect"]!);
        await Json(await parent.PostAsJsonAsync($"/api/family/kids/{kidId}/answers", new
        {
            sessionId = session, mode = "Practice", source = "Topic", title = "Multiply", localDate = Today, elapsedSeconds = 40,
            responses = new[] { new { questionId = q2, response = "20" } },
        }));
        var finished = await Json(await parent.PostAsync($"/api/family/kids/{kidId}/sessions/{session}/finish", null));
        Assert.Equal((1, 2), ((int)finished["correct"]!, (int)finished["total"]!));

        var summary = await parent.GetFromJsonAsync<JsonObject>($"/api/family/kids/{kidId}/summary?localDate={Today}");
        Assert.Equal((1, 1, 1), ((int)summary!["streak"]!, (int)summary["stars"]!, (int)summary["mistakesToFix"]!));

        var mistakes = await parent.GetFromJsonAsync<JsonObject>($"/api/family/kids/{kidId}/mistakes");
        Assert.Equal("Mistakes", (string)mistakes!["source"]!);
        Assert.Equal(q2, (string)Assert.Single(mistakes["questions"]!.AsArray())!["id"]!);
        // The set never carries the key.
        Assert.Null(mistakes["questions"]![0]!["answer"]);

        var dashboard = await Json(await parent.GetAsync($"/api/family/kids/{kidId}/dashboard?localDate={Today}"));
        Assert.Equal((2, 1, 1), ((int)dashboard["answered"]!, (int)dashboard["correct"]!, (int)dashboard["week"]!["daysPracticed"]!));
        Assert.Equal("Multiply", (string)dashboard["recentSessions"]![0]!["title"]!);
    }

    [Fact]
    public async Task The_parent_pin_guards_the_dashboard_and_managing_kids()
    {
        var (courseId, _, _) = await ImportAsync();
        var (parent, kidId) = await FamilyAsync(courseId);

        var gate = await parent.GetFromJsonAsync<JsonObject>("/api/family/pin");
        Assert.False((bool)gate!["hasPin"]!);
        Assert.NotNull(gate["parentAreaOpenUntil"]); // a fresh sign-in opens the parent areas

        await Json(await parent.PutAsJsonAsync("/api/family/pin", new { pin = "2468" }));
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync("/api/family/pin/lock", null)).StatusCode);

        // Locked: the kid can still practice, but the parent areas are closed.
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/family/kids/{kidId}/summary")).StatusCode);
        var dashboard = await parent.GetAsync($"/api/family/kids/{kidId}/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);
        Assert.Contains("parent.locked", await dashboard.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsJsonAsync("/api/family/kids", new { name = "Leo", avatar = "Blue" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.DeleteAsync($"/api/family/kids/{kidId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PutAsJsonAsync("/api/family/pin", new { pin = "1111" })).StatusCode);

        var wrong = await Json(await parent.PostAsJsonAsync("/api/family/pin/unlock", new { pin = "0000" }));
        Assert.Equal(("Wrong", 4), ((string)wrong["result"]!, (int)wrong["triesLeft"]!));
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/family/kids/{kidId}/dashboard")).StatusCode);

        var right = await Json(await parent.PostAsJsonAsync("/api/family/pin/unlock", new { pin = "2468" }));
        Assert.Equal("Unlocked", (string)right["result"]!);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/family/kids/{kidId}/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Another_family_cannot_see_or_record_for_a_kid()
    {
        var (courseId, q1, _) = await ImportAsync();
        var (_, kidId) = await FamilyAsync(courseId);
        var sam = _factory.CreateClient();
        await Json(await sam.PostAsJsonAsync("/api/auth/developer", new { email = "sam@example.org" }));

        Assert.Equal(HttpStatusCode.NotFound, (await sam.GetAsync($"/api/family/kids/{kidId}/dashboard")).StatusCode);
        var record = await sam.PostAsJsonAsync($"/api/family/kids/{kidId}/answers", new
        {
            sessionId = Guid.NewGuid(), mode = "Practice", responses = new[] { new { questionId = q1, response = "24" } },
        });
        Assert.Equal(HttpStatusCode.NotFound, record.StatusCode);
    }
}
