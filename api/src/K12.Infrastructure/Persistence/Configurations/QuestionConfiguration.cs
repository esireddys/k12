using K12.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.ToTable("Questions", "content");
        b.HasKey(q => q.Id);
        b.Property(q => q.Id).HasConversion(Converters.QuestionId).ValueGeneratedNever();

        // Skill ids as a JSON array; EF translates "has skill X" queries with OPENJSON.
        b.PrimitiveCollection(q => q.SkillIds)
            .HasField("_skillIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType(e => e.HasConversion(Converters.SkillId));

        b.Property(q => q.ItemType).HasMaxLength(40).IsRequired();
        b.Property(q => q.ContentJson).IsRequired();
        b.Property(q => q.Answer)
            .HasConversion(Converters.AnswerSpec, Converters.AnswerSpecComparer)
            .HasColumnName("AnswerSpecJson")
            .IsRequired();
        b.Property(q => q.Difficulty).HasConversion<string>().HasMaxLength(20);
        b.Property(q => q.Origin).HasConversion<string>().HasMaxLength(20);
        b.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(q => q.ParentId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new QuestionId(v.Value) : null);
        b.Property(q => q.Version).IsConcurrencyToken();

        b.HasIndex(q => q.Status);
        b.Ignore(q => q.DomainEvents);
        b.Ignore(q => q.IsVisibleToStudents);
    }
}
