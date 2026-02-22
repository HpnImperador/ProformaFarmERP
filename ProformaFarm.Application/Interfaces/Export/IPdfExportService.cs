using System.Collections.Generic;

namespace ProformaFarm.Application.Interfaces.Export;

public interface IPdfExportService
{
    byte[] BuildSimpleReport(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows);
}
