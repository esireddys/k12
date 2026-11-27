using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using K12.Api.Auth;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Api.Tests;

public sealed class ContentReviewApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ContentReviewApiTests(ApiFactory factory) => _factory = factory;

    private HttpClient Client(string? asUser = null)
    {
        var client = _factory.CreateClient();
        if (asUser is not null) client.DefaultRequestHeaders.Add(Access.DevelopmentUserHeader, asUser);
        return client;
    }

    [Fact]
    public void Tests_never_use_a_real_database() => Assert.True(_factory.UsesInMemoryStore());

    [Theory]
    [InlineData(null, "owner@localhost", true)]
    [InlineData("parent@example.org", "parent@example.org", false)]
    public async Task Me_says_who_is_signed_in_and_whether_they_are_an_owner(string? asUser, string name, bool isOwner)
    {
        var me = await Client(asUser).GetFromJsonAsync<JsonObject>("/api/me");
        Assert.Equal((name, isOwner), ((string)me!["name"]!, (bool)me["isOwner"]!));
    }

    [Theory]
    [InlineData("GET", "/api/admin/review/queue")]
    [InlineData("GET", "/api/admin/review/ai-queue")]
    [InlineData("GET", "/api/admin/review/questions/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/admin/review/questions/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/admin/review/questions/00000000-0000-0000-0000-000000000001/reject")]
    [InlineData("POST", "/api/admin/review/questions/00000000-0000-0000-0000-000000000001/ai-review")]
    [InlineData("GET", "/api/admin/review/images/form/01.png")]
    public async Task Parents_are_refused_every_review_endpoint(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") request.Content = JsonContent.Create(new { version = 1, reason = "x" });
        var response = await Client("parent@example.org").SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Outside_development_nobody_is_signed_in_yet()
    {
        using var production = new ApiFactory { Environment = "Production" };
        var response = await production.CreateClient().GetAsync("/api/admin/review/queue");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_imported_released_item_is_live_straight_away_and_skips_both_review_queues()
    {
        var client = Client();
        var exam = $"EXAM{Guid.NewGuid():N}"[..12];
        (await client.PostAsJsonAsync("/api/admin/import/course", new
        {
            jurisdiction = "TX", subject = "math", code = "G3", name = "Grade 3", frameworkCode = "FW", frameworkName = "Framework",
            categories = new[] { new { number = 1, name = "Numbers" } },
            skills = new[] { new { code = "3.4(K)", description = "Solve", category = 1, designation = "readiness" } },
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/admin/import/exam-form", new
        {
            jurisdiction = "TX", subject = "math", courseCode = "G3", exam, year = 2022, season = "spring",
            sourceUrl = "https://example.org/t.pdf",
            items = new[]
            {
                new
                {
                    number = 1, skillCodes = new[] { "3.4(K)" }, itemType = "numeric",
                    content = new { image = "form/01.png", stem = "6 x 4 = ?", textNeedsReview = true },
                    answer = new { kind = "Integer", value = "24" },
                },
            },
        })).EnsureSuccessStatusCode();

        var course = await _factory.Services.GetRequiredService<ICourseRepository>().FindAsync("TX", "math", "G3");
        var form = await _factory.Services.GetRequiredService<IExamFormRepository>().FindAsync(course!.Id, exam, 2022, "spring");
        var id = Assert.Single(form!.Items).QuestionId.Value.ToString();

        var aiQueue = await client.GetFromJsonAsync<JsonObject>("/api/admin/review/ai-queue?take=500");
        Assert.DoesNotContain(aiQueue!["items"]!.AsArray(), i => (string)i!["id"]! == id);
        var ownerQueue = await client.GetFromJsonAsync<JsonObject>("/api/admin/review/queue");
        Assert.DoesNotContain(ownerQueue!["items"]!.AsArray(), i => (string)i!["id"]! == id);

        var detail = await client.GetFromJsonAsync<JsonObject>($"/api/admin/review/questions/{id}");
        Assert.Equal(("Live", "Official"), ((string)detail!["status"]!, (string)detail["origin"]!));

        var check = await client.PostAsJsonAsync($"/api/practice/questions/{id}/check", new { response = "24" });
        Assert.True((await check.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isCorrect").GetBoolean());

        var publish = await client.PostAsync("/api/admin/import/publish-released", null);
        publish.EnsureSuccessStatusCode();
        Assert.Equal(0, (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("questionsPublished").GetInt32());
    }

    [Fact]
    public async Task Owners_get_item_pictures_and_nothing_outside_the_picture_folder()
    {
        Directory.CreateDirectory(Path.Combine(_factory.ImageRoot, "form"));
        await File.WriteAllBytesAsync(Path.Combine(_factory.ImageRoot, "form", "01.png"), [137, 80, 78, 71]);

        var ok = await Client().GetAsync("/api/admin/review/images/form/01.png");
        var encoded = await Client().GetAsync("/api/admin/review/images/form%2F01.png");
        var climb = await Client().GetAsync("/api/admin/review/images/..%2F..%2Fetc%2Fpasswd.png");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, encoded.StatusCode);
        Assert.Equal("image/png", ok.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, climb.StatusCode);
    }
}
