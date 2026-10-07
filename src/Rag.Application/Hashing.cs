using System.Security.Cryptography;
using System.Text;

namespace Rag.Application;

public static class Hashing
{
    public static string Sha256(string text) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
}
