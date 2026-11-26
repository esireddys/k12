using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace K12.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` build the model without starting the API.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<K12DbContext>
{
    public K12DbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<K12DbContext>()
            .UseSqlServer("Server=localhost;Database=K12;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
