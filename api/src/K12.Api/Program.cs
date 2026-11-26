using System.Text.Json.Serialization;
using K12.Core.Answers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Temporary: checks a response against a spec sent by the caller.
// Once the question bank exists, this takes a question id and loads the spec server-side,
// so answers are never sent to the device.
app.MapPost("/api/check", (CheckRequest request) => AnswerChecker.Check(request.Spec, request.Response))
   .WithName("CheckAnswer");

app.Run();

public sealed record CheckRequest(AnswerSpec Spec, string? Response);

public partial class Program;
