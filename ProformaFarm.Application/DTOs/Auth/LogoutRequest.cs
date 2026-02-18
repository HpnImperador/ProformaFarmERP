using System;
using System.Collections.Generic;
using System.Text;

namespace ProformaFarm.Application.DTOs.Auth;

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}

