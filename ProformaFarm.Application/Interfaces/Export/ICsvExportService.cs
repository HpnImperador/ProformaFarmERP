using System.Collections.Generic;

namespace ProformaFarm.Application.Interfaces.Export;

public interface ICsvExportService
{
    string BuildCsv<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<(string Header, System.Func<T, object?> ValueSelector)> columns);
}
