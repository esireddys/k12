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
        b.Property(s => s.Code).HasMaxLength(40).IsRequired();
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.LimitsJson).IsRequired();
        b.Ignore(s => s.DomainEvents);
    }
}
