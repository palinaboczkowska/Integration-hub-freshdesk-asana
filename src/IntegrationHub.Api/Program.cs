using IntegrationHub.Api.Integrations.Freshdesk;
using IntegrationHub.Api.Integrations.Asana;
using IntegrationHub.Api.Models;
using IntegrationHub.Api.Services;
using IntegrationHub.Api.Data;
using IntegrationHub.Api.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<IntegrationSettings>(
    builder.Configuration.GetSection("IntegrationSettings"));

builder.Services.AddControllers();

builder.Services.AddDbContext<IntegrationHubDbContext>(options =>
    options.UseSqlite("Data Source=integrationhub.db"));

builder.Services.AddScoped<IIntegrationService, IntegrationService>();
builder.Services.AddScoped<IMappingRepository, EfMappingRepository>();
builder.Services.AddScoped<ITicketSyncService, TicketSyncService>();
builder.Services.AddScoped<IAsanaToFreshdeskSyncService, AsanaToFreshdeskSyncService>();

builder.Services.AddHttpClient<FreshdeskClient>();
builder.Services.AddHttpClient<AsanaClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>().Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
