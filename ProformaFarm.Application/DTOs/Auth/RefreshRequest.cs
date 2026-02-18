using System;
using System.Collections.Generic;
using System.Text;

namespace ProformaFarm.Application.DTOs.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = default!;
}

