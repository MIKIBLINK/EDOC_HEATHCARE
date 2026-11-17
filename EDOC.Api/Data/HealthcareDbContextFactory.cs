using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EDOC.Api.Data;

public sealed class HealthcareDbContextFactory : IDesignTimeDbContextFactory<HealthcareDbContext>
{
    public HealthcareDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EDOC_CONNECTION_STRING")
            ?? "Server=localhost\\SQLEXPRESS;Database=EDOC_HealthcareDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<HealthcareDbContext>().UseSqlServer(connectionString).Options;
        return new HealthcareDbContext(options);
    }
}
