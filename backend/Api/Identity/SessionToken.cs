using System.Security.Cryptography;
using System.Text;

namespace ProjectVTT.Api.Identity;

/// <summary>Generación y hash de tokens de sesión (aleatorios, 256 bits).</summary>
public static class SessionToken
{
    /// <summary>Token opaco para el cliente (hex, 64 caracteres).</summary>
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Hash SHA-256 del token para almacenarlo: si la base de datos se
    /// filtra, los tokens no son utilizables. SHA-256 es apropiado aquí
    /// porque el token ya es aleatorio de alta entropía (a diferencia de
    /// una contraseña, que se guarda con PBKDF2 vía PasswordHasher).
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
