using System;
using System.Data;

namespace ProformaFarm.Application.Interfaces.Data;

public interface ISqlConnectionFactory
{
    string ProviderName { get; }
    IDbConnection CreateConnection();
}
