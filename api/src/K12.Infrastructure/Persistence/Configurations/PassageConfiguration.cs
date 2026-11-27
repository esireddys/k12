using K12.Domain.Content;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class PassageConfiguration : IEntityTypeConfiguration<Passage>
{
    public void Configure(EntityTypeBuilder<Passage> b)
    {
        b.ToTable("Passages", "content");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasConversion(Converters.PassageId).ValueGeneratedNever();
        b.Property(p => p.CourseId).HasConversion(Converters.CourseId);
        b.HasOne<Course>().WithMany().HasForeignKey(p => p.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(p => p.Title).HasMaxLength(Passage.MaxTitleLength).IsRequired();
        b.Property(p => p.ContentJson).IsRequired();
        b.Property(p => p.Origin).HasConversion<string>().HasMaxLength(20);
        b.Ignore(p => p.DomainEvents);
    }
}
