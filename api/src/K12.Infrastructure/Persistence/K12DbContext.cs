using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

/// <summary>The SQL Server database. Also the unit of work: one SaveChanges per use case.</summary>
public sealed class K12DbContext(DbContextOptions<K12DbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ExamForm> ExamForms => Set<ExamForm>();
    public DbSet<ParentAccount> ParentAccounts => Set<ParentAccount>();
    public DbSet<KidProgress> KidProgress => Set<KidProgress>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(K12DbContext).Assembly);

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new ConcurrencyConflictException(e);
        }
    }
}
