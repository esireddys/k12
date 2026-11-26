using System.Text.Json.Serialization;
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
     .AllowAnyMethod()));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    var sampleId = await DevelopmentSeed.RunAsync(app.Services);
    app.Logger.LogInformation("Seeded sample question {QuestionId}", sampleId?.Value);
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPracticeEndpoints();

app.Run();

public partial class Program;
