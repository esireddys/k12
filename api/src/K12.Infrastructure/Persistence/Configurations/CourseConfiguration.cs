using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("Courses", "curriculum");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(Converters.CourseId).ValueGeneratedNever();
        b.Property(c => c.Jurisdiction).HasMaxLength(10).IsRequired();
        b.Property(c => c.JurisdictionName).HasMaxLength(100).IsRequired();
        b.Property(c => c.Subject).HasMaxLength(40).IsRequired();
        b.Property(c => c.Code).HasMaxLength(20).IsRequired();
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.FrameworkCode).HasMaxLength(40).IsRequired();
        b.Property(c => c.FrameworkName).HasMaxLength(200).IsRequired();
        b.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.TypicalGrade);
        b.HasIndex(c => new { c.Jurisdiction, c.Subject, c.Code }).IsUnique();

        b.OwnsMany(c => c.Categories, cat =>
        {
            cat.ToTable("ReportingCategories", "curriculum");
            cat.WithOwner().HasForeignKey("CourseId");
            cat.HasKey("CourseId", nameof(ReportingCategory.Number));
            cat.Property(x => x.Number).ValueGeneratedNever();
            cat.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        b.Navigation(c => c.Categories).HasField("_categories").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(c => c.DomainEvents);
    }
}
