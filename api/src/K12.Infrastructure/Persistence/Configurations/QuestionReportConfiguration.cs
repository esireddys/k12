using K12.Domain.Content;
using K12.Domain.Content.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> b)
    {
        b.ToTable("QuestionReports", "content");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasConversion(Converters.QuestionReportId).ValueGeneratedNever();
        b.Property(r => r.QuestionId).HasConversion(Converters.QuestionId);
        b.HasOne<Question>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Property(r => r.QuestionVersion);
        b.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Reporter).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.ReportedAt);
        b.Property(r => r.ClosedBy).HasMaxLength(200);
        b.Property(r => r.ClosedAt);
        b.HasIndex(r => new { r.ClosedAt, r.ReportedAt });
        b.HasIndex(r => r.QuestionId);
        b.Ignore(r => r.IsOpen);
        b.Ignore(r => r.DomainEvents);
    }
}
