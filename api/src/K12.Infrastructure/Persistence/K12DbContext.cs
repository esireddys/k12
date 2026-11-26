using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

/// <summary>The SQL Server database. Also the unit of work: one SaveChanges per use case.</summary>
public sealed class K12DbContext(DbContextOptions<K12DbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ExamForm> ExamForms => Set<ExamForm>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(K12DbContext).Assembly);

    Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);
}
