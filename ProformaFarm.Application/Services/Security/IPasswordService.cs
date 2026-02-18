using System;

namespace ProformaFarm.Application.Services.Security;

public interface IPasswordService
{
    (string HashBase64, string SaltBase64) HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hashBase64, string? saltBase64);
}


