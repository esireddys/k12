using K12.Domain.Content;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class ExamFormConfiguration : IEntityTypeConfiguration<ExamForm>
{
    public void Configure(EntityTypeBuilder<ExamForm> b)
    {
        b.ToTable("ExamForms", "content");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasConversion(Converters.ExamFormId).ValueGeneratedNever();
        b.Property(f => f.CourseId).HasConversion(Converters.CourseId);
        b.HasOne<Course>().WithMany().HasForeignKey(f => f.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(f => f.Exam).HasMaxLength(40).IsRequired();
        b.Property(f => f.Season).HasMaxLength(20).IsRequired();
        b.Property(f => f.SourceUrl).HasMaxLength(500).IsRequired();
        b.HasIndex(f => new { f.CourseId, f.Exam, f.Year, f.Season }).IsUnique();

        b.OwnsMany(f => f.Items, item =>
        {
            item.ToTable("ExamFormItems", "content");
            item.WithOwner().HasForeignKey("ExamFormId");
            item.HasKey("ExamFormId", nameof(ExamFormItem.Number));
            item.Property(x => x.Number).ValueGeneratedNever();
            item.Property(x => x.QuestionId).HasConversion(Converters.QuestionId);
            item.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(f => f.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(f => f.DomainEvents);
    }
}
