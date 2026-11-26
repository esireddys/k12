using System.Text.Json;
using System.Text.Json.Serialization;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace K12.Infrastructure.Persistence.Configurations;

internal static class Converters
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly ValueConverter<QuestionId, Guid> QuestionId = new(id => id.Value, v => new QuestionId(v));

    public static readonly ValueConverter<SkillId, Guid> SkillId = new(id => id.Value, v => new SkillId(v));

    /// <summary>Stores the answer spec as JSON in an nvarchar(max) column.</summary>
    public static readonly ValueConverter<AnswerSpec, string> AnswerSpec = new(
        spec => JsonSerializer.Serialize(spec, Json),
        json => JsonSerializer.Deserialize<AnswerSpec>(json, Json)!);

    public static readonly ValueComparer<AnswerSpec> AnswerSpecComparer = new(
        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
        a => JsonSerializer.Serialize(a, Json).GetHashCode(),
        a => JsonSerializer.Deserialize<AnswerSpec>(JsonSerializer.Serialize(a, Json), Json)!);
}
