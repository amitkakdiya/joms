using Microsoft.Data.SqlClient;
using System.Data;

namespace Joms.Infrastructure;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}

public sealed class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

    public SqlConnection CreateConnection() => new(_connectionString);
}
