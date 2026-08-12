using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManagerAPI.Data;

namespace TaskManagerAPI.Health
{
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(AppDbContext context, ILogger<DatabaseHealthCheck> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _context.Database.CanConnectAsync(cancellationToken))
                {
                    return HealthCheckResult.Healthy();
                }

                _logger.LogWarning("Database readiness check failed");
                return HealthCheckResult.Unhealthy("Database connection failed.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Database readiness check raised an exception");
                return HealthCheckResult.Unhealthy("Database connection failed.", exception);
            }
        }
    }
}
