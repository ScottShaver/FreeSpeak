using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Design-time factory for creating ApplicationDbContext instances during EF Core migrations and tooling operations.
    /// Implements IDesignTimeDbContextFactory to support 'dotnet ef' commands by providing the DbContext configuration.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        /// <summary>
        /// Creates a new instance of ApplicationDbContext for design-time operations.
        /// This method is called by EF Core tooling (migrations, database updates, etc.) to obtain a DbContext instance.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the EF Core tools.</param>
        /// <returns>A configured ApplicationDbContext instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the connection string is not found in configuration or user secrets.</exception>
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Build configuration to read from appsettings.json and user secrets
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets("aspnet-FreeSpeakWeb-1df4328d-3596-4e25-8ed5-936ffafdff2c")
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration or user secrets.");

            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
