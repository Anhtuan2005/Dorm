using System.Text.RegularExpressions;
using DormitoryManagementSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DormitoryManagementSystem.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(SqlServerTestDatabase.AdminConnectionString))
            Skip = "Set DORM_TEST_SQL_ADMIN_CONNECTION to run SQL Server integration tests.";
    }
}

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string DatabasePrefix = "DormitoryManagementSystem_Test_";
    private static readonly Regex SafeDatabaseName = new(
        $"^{DatabasePrefix}[a-f0-9]{{32}}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string adminConnectionString;
    private readonly string databaseName;
    private readonly DbContextOptions<DormitoryDbContext> options;

    private SqlServerTestDatabase(string adminConnectionString, string databaseName)
    {
        this.adminConnectionString = adminConnectionString;
        this.databaseName = databaseName;
        var builder = new SqlConnectionStringBuilder(adminConnectionString) { InitialCatalog = databaseName };
        options = new DbContextOptionsBuilder<DormitoryDbContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.EnableRetryOnFailure())
            .Options;
    }

    public static string? AdminConnectionString =>
        Environment.GetEnvironmentVariable("DORM_TEST_SQL_ADMIN_CONNECTION");

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var connection = AdminConnectionString
            ?? throw new InvalidOperationException("Missing DORM_TEST_SQL_ADMIN_CONNECTION.");
        var database = new SqlServerTestDatabase(connection, $"{DatabasePrefix}{Guid.NewGuid():N}");
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public DormitoryDbContext CreateContext() => new(options);

    public async ValueTask DisposeAsync()
    {
        if (!SafeDatabaseName.IsMatch(databaseName))
            throw new InvalidOperationException("Refusing to drop an unsafe integration-test database name.");

        var builder = new SqlConnectionStringBuilder(adminConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
