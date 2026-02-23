using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using ProformaFarm.Application.Interfaces.Data;

namespace ProformaFarm.Infrastructure.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _provider;
    private readonly string _connectionString;
    public string ProviderName => _provider;

    public SqlConnectionFactory(string provider, string connectionString)
    {
        _provider = string.IsNullOrWhiteSpace(provider)
            ? "SqlServer"
            : provider.Trim();
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IDbConnection CreateConnection()
        => _provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
           || _provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            ? new NpgsqlConnection(_connectionString)
            : new SqlConnection(_connectionString);
}
