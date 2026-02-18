using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ProformaFarm.Infrastructure.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connString = configuration.GetConnectionString("ProformaFarm")
            ?? throw new InvalidOperationException("ConnectionString 'ProformaFarm' não encontrada.");
    }

    public IDbConnection CreateConnection()
        => new SqlConnection(_connString);
}
