using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProformaFarm.Application.DTOs.Auth;

public sealed record JwtTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc
);

