using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Context;

public interface IOrgContext
{
    int? GetCurrentUsuarioId();
    bool IsRequestedOrganizacaoHeaderProvided();
    bool IsRequestedOrganizacaoHeaderInvalid();
    int? GetRequestedOrganizacaoId();
    Task<int?> GetCurrentOrganizacaoIdAsync(CancellationToken ct = default);
    Task<int?> GetCurrentUnidadeIdAsync(CancellationToken ct = default);
    Task<bool> HasAccessToOrganizacaoAsync(int idOrganizacao, CancellationToken ct = default);
}
