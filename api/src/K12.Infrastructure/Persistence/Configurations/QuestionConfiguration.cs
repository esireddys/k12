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
        // Status is checked on save as well as Version: two owners deciding at once, or an owner and the AI reviewer,
        // change the status without changing the version, and the second save must fail rather than overwrite.
        b.Property(q => q.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
        b.Property(q => q.ParentId).HasConversion(
            id => id.HasValue ? id.Value.Value : (Guid?)null,
            v => v.HasValue ? new QuestionId(v.Value) : null);
        b.Property(q => q.Version).IsConcurrencyToken();

        b.Property(q => q.AiReview)
            .HasConversion(Converters.AiReview!, Converters.AiReviewComparer)
            .HasColumnName("AiReviewJson");

        // Who decided, when and why sit in their own columns so they can be audited in SSMS.
        b.OwnsOne(q => q.OwnerDecision, d =>
        {
            d.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).HasColumnName("OwnerOutcome");
            d.Property(x => x.DecidedBy).HasMaxLength(200).HasColumnName("OwnerDecidedBy");
            d.Property(x => x.DecidedAt).HasColumnName("OwnerDecidedAt");
            d.Property(x => x.Note).HasMaxLength(2000).HasColumnName("OwnerNote");
        });

        b.HasIndex(q => q.Status);
        b.Ignore(q => q.DomainEvents);
        b.Ignore(q => q.IsVisibleToStudents);
    }
}
