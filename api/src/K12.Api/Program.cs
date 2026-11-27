using System.Text.Json.Serialization;
using K12.Api.Auth;
using K12.Api.Endpoints;
using K12.Application;
using K12.Infrastructure;
using K12.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials())); // the session cookie travels with the web app's requests

// Content:ImageRoot may be relative to the API project (the default points at the pipeline's data/out).
if (builder.Configuration["Content:ImageRoot"] is { Length: > 0 } imageRoot)
    builder.Configuration["Content:ImageRoot"] = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, imageRoot));

builder.Services
    .AddK12Access(builder.Environment, builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("K12")))
    app.Logger.LogWarning(
        "ConnectionStrings:K12 is not set, so the API is using an in-memory store: nothing is saved when it stops. " +
        "Set it with dotnet user-secrets (see api/LOCAL_DATABASE.md).");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    var sampleId = await DevelopmentSeed.RunAsync(app.Services);
    app.Logger.LogInformation("Seeded sample question {QuestionId}", sampleId?.Value);
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccountEndpoints(app.Environment);
app.MapPracticeEndpoints();
app.MapProgressEndpoints();
app.MapContentReviewEndpoints();
if (app.Environment.IsDevelopment()) app.MapContentImportEndpoints(); // admin role once sign-in exists

app.Run();

public partial class Program;
