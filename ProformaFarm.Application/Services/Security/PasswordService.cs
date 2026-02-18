using System.Security.Cryptography;

namespace ProformaFarm.Application.Services.Security;

public sealed class PasswordService : IPasswordService
{
    private const int SaltSize = 16;     // 128-bit
    private const int KeySize = 32;     // 256-bit
    private const int Iterations = 100_000;

    public (string HashBase64, string SaltBase64) HashPassword(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: plainPassword,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string plainPassword, string hashBase64, string? saltBase64)
    {
        if (string.IsNullOrWhiteSpace(saltBase64))
            return false; // padrão: não aceita legado

        var salt = Convert.FromBase64String(saltBase64);

        var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
            password: plainPassword,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        var expected = Convert.FromBase64String(hashBase64);

        return CryptographicOperations.FixedTimeEquals(hashToCompare, expected);
    }
}
