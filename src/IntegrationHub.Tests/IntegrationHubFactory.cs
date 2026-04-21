using IntegrationHub.Api.Data;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationHub.Tests;

public class IntegrationHubFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ihub-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IntegrationHubDbContext>>();
            services.AddDbContext<IntegrationHubDbContext>(opts =>
                opts.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    public Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>().Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        return Task.CompletedTask;
    }
}
