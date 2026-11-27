using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

/// <summary>A kid's summaries: mastery per skill, mistakes to fix and practice days, in the progress schema.</summary>
internal sealed class KidProgressConfiguration : IEntityTypeConfiguration<KidProgress>
{
    public void Configure(EntityTypeBuilder<KidProgress> b)
    {
        b.ToTable("KidProgress", "progress");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("KidId").HasConversion(Converters.KidProfileId).ValueGeneratedNever();
        b.Ignore(p => p.KidId);
        b.Ignore(p => p.TotalAnswered);
        b.Ignore(p => p.TotalCorrect);
        b.Ignore(p => p.OpenMistakes);

        b.OwnsMany(p => p.Skills, s =>
        {
            s.ToTable("SkillProgress", "progress");
            s.WithOwner().HasForeignKey("KidId");
            s.Property(x => x.Id).HasColumnName("SkillId").HasConversion(Converters.SkillId).ValueGeneratedNever();
            s.HasKey("KidId", nameof(SkillProgress.Id));
            s.Ignore(x => x.SkillId);
            s.Ignore(x => x.Level);
            s.Property(x => x.Recent).HasMaxLength(Mastery.Window).IsUnicode(false).IsRequired();
            s.HasOne<Skill>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(p => p.Skills).HasField("_skills").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(p => p.Mistakes, m =>
        {
            m.ToTable("Mistakes", "progress");
            m.WithOwner().HasForeignKey("KidId");
            m.Property(x => x.Id).HasColumnName("QuestionId").HasConversion(Converters.QuestionId).ValueGeneratedNever();
            m.HasKey("KidId", nameof(Mistake.Id));
            m.Ignore(x => x.QuestionId);
            m.Ignore(x => x.IsOpen);
            m.Property(x => x.SkillId).HasConversion(Converters.OptionalSkillId);
            m.HasOne<Question>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(p => p.Mistakes).HasField("_mistakes").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(p => p.Days, d =>
        {
            d.ToTable("PracticeDays", "progress");
            d.WithOwner().HasForeignKey("KidId");
            d.Property(x => x.Id).HasColumnName("Date").ValueGeneratedNever();
            d.HasKey("KidId", nameof(PracticeDay.Id));
            d.Ignore(x => x.Date);
        });
        b.Navigation(p => p.Days).HasField("_days").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(p => p.DomainEvents);
    }
}

/// <summary>A kid's attempt history: one row per set, one per answer.</summary>
internal sealed class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    public void Configure(EntityTypeBuilder<PracticeSession> b)
    {
        b.ToTable("PracticeSessions", "progress");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasConversion(Converters.PracticeSessionId).ValueGeneratedNever();
        b.Property(s => s.KidId).HasConversion(Converters.KidProfileId);
        b.Property(s => s.CourseId).HasConversion(Converters.CourseId);
        b.HasOne<Course>().WithMany().HasForeignKey(s => s.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.Mode).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Source).HasMaxLength(20).IsRequired();
        b.Property(s => s.Title).HasMaxLength(PracticeSession.MaxTitleLength).IsRequired();
        b.HasIndex(s => new { s.KidId, s.StartedAt });
        b.Ignore(s => s.IsFinished);
        b.Ignore(s => s.Correct);
        b.Ignore(s => s.Total);
        b.Ignore(s => s.Minutes);

        b.OwnsMany(s => s.Answers, a =>
        {
            a.ToTable("SessionAnswers", "progress");
            a.WithOwner().HasForeignKey("SessionId");
            a.Property(x => x.Id).HasColumnName("QuestionId").HasConversion(Converters.QuestionId).ValueGeneratedNever();
            a.HasKey("SessionId", nameof(SessionAnswer.Id));
            a.Ignore(x => x.QuestionId);
            a.Ignore(x => x.Answered);
            a.Property(x => x.SkillId).HasConversion(Converters.OptionalSkillId);
            a.Property(x => x.Response).HasMaxLength(SessionAnswer.MaxResponseLength).IsRequired();
            a.HasOne<Question>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(s => s.Answers).HasField("_answers").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(s => s.DomainEvents);
    }
}
