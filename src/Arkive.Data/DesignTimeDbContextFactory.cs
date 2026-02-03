using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Arkive.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArkiveDbContext>
{
    public ArkiveDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ArkiveDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Arkive;Trusted_Connection=true;TrustServerCertificate=true;");

        return new ArkiveDbContext(optionsBuilder.Options);
    }
}
