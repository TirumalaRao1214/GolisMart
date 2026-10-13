using Microsoft.Data.SqlClient;
using System.Data;

namespace GOLISMart.API.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("GOLISMartDb")
            ?? throw new InvalidOperationException("Connection string 'GOLISMartDb' is not configured.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
