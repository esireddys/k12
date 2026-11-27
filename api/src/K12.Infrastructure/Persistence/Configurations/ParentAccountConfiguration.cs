using K12.Domain.Accounts;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K12.Infrastructure.Persistence.Configurations;

internal sealed class ParentAccountConfiguration : IEntityTypeConfiguration<ParentAccount>
{
    public void Configure(EntityTypeBuilder<ParentAccount> b)
    {
        b.ToTable("ParentAccounts", "accounts");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(Converters.ParentAccountId).ValueGeneratedNever();

        // The provider's subject id identifies the parent; their email can change.
        b.OwnsOne(a => a.Login, l =>
        {
            l.Property(x => x.Provider).HasMaxLength(40).HasColumnName("LoginProvider").IsRequired();
            l.Property(x => x.Subject).HasMaxLength(200).HasColumnName("LoginSubject").IsRequired();
            l.HasIndex(x => new { x.Provider, x.Subject }).IsUnique();
        });
        b.Navigation(a => a.Login).IsRequired();

        b.Property(a => a.Email).HasMaxLength(320).IsRequired();
        b.Property(a => a.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(a => a.CreatedAt);
        b.Property(a => a.LastSignedInAt);

        // Kid profiles hold a name, a colour and a course only: no email, photo or location.
        b.OwnsMany(a => a.Kids, kid =>
        {
            kid.ToTable("KidProfiles", "accounts");
            kid.WithOwner().HasForeignKey("ParentAccountId");
            kid.HasKey(k => k.Id);
            kid.Property(k => k.Id).HasConversion(Converters.KidProfileId).ValueGeneratedNever();
            kid.Property(k => k.Name).HasMaxLength(KidProfile.MaxNameLength).IsRequired();
            kid.Property(k => k.Avatar).HasConversion<string>().HasMaxLength(20);
            kid.Property(k => k.CourseId).HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new CourseId(v.Value) : null);
            kid.HasOne<Course>().WithMany().HasForeignKey(k => k.CourseId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Navigation(a => a.Kids).HasField("_kids").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Only a salted hash of the parent PIN is kept, with the wrong-try count that locks it.
        b.OwnsOne(a => a.Pin, pin =>
        {
            pin.Property(p => p.Hash).HasColumnName("PinHash").HasMaxLength(200).IsRequired();
            pin.Property(p => p.SetAt).HasColumnName("PinSetAt");
            pin.Property(p => p.WrongTries).HasColumnName("PinWrongTries");
            pin.Property(p => p.LockedUntil).HasColumnName("PinLockedUntil");
        });
        b.Ignore(a => a.HasPin);

        b.Ignore(a => a.DomainEvents);
    }
}
