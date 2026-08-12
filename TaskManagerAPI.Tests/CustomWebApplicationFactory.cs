using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagerAPI.Data;

namespace TaskManagerAPI.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestJwtKey = "integration-tests-only-fake-jwt-signing-key-never-used-in-prod";

    // Named shared-cache in-memory database: cada AppDbContext abre sua PRÓPRIA SqliteConnection
    // (via connection string), permitindo transações concorrentes reais com locking de verdade do
    // SQLite. Reusar um único objeto SqliteConnection entre requisições (como antes) serializa toda
    // transação no nível do ADO.NET ("transaction within a transaction"), mascarando concorrência real.
    // A conexão de keep-alive só existe pra manter o banco :memory: vivo entre requisições — sem ela,
    // o banco seria destruído assim que a última conexão real fechasse.
    private readonly string _connectionString = $"Data Source=file:rt-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAliveConnection;

    public CustomWebApplicationFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing environment skips user-secrets loading (only added for Development),
        // so integration tests never depend on secrets configured on the developer's machine.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "TaskManagerAPI.Tests",
                ["Jwt:Audience"] = "TaskManagerAPI.Tests",
                ["Jwt:ExpiresInMinutes"] = "60",
                ["Jwt:RefreshTokenExpiresInDays"] = "7"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
