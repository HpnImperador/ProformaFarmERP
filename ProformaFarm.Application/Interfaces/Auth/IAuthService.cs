using System.Threading;
using System.Threading.Tasks;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.DTOs.Auth; // UNICA FONTE DE DADOS

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest req, string? ip, CancellationToken ct = default);
    Task<ApiResponse<LoginResponse>> RefreshAsync(RefreshRequest req, string? ip, CancellationToken ct = default);
    Task<ApiResponse<object>> LogoutAsync(LogoutRequest req, string? ip, CancellationToken ct = default);
}