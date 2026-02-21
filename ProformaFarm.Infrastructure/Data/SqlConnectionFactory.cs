using System;
using System.Data;
using Microsoft.Data.SqlClient;
using ProformaFarm.Application.Interfaces.Data;

namespace ProformaFarm.Infrastructure.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IDbConnection CreateConnection()
        => new SqlConnection(_connectionString);
}
