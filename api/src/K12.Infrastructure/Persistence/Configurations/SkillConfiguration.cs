using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.ToTable("Skills", "curriculum");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasConversion(Converters.SkillId).ValueGeneratedNever();
        b.Property(s => s.CourseId).HasConversion(Converters.CourseId);
        b.HasOne<Course>().WithMany().HasForeignKey(s => s.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(s => new { s.CourseId, s.Code }).IsUnique();
        b.Property(s => s.Designation).HasMaxLength(40);
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.LimitsJson).IsRequired();
        b.Ignore(s => s.DomainEvents);
    }
}
