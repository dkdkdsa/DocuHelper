using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    internal static class PasswordHash
    {
        private const int LegacySeed = 0xCE4B;

        public const int DefaultSpinCount = 100000;

        public static string Legacy(string password)
        {
            var text = password ?? string.Empty;
            var hash = 0;
            for (var index = 1; index <= text.Length; index++)
            {
                var value = text[index - 1] << index;
                var rotated = value >> 15;
                value &= 0x7FFF;
                hash ^= value | rotated;
            }

            hash ^= text.Length;
            hash ^= LegacySeed;
            return hash.ToString("X4", CultureInfo.InvariantCulture);
        }

        public static bool MatchesLegacy(string password, string hash) =>
            !string.IsNullOrEmpty(hash) && string.Equals(Legacy(password), hash.Trim(), StringComparison.OrdinalIgnoreCase);

        public static bool MatchesSha512(string password, string algorithm, string saltBase64, string hashBase64, int spinCount)
        {
            if (!string.Equals(algorithm, "SHA-512", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(saltBase64) || string.IsNullOrEmpty(hashBase64))
                return false;

            byte[] salt;
            try
            {
                salt = Convert.FromBase64String(saltBase64);
            }
            catch (FormatException)
            {
                return false;
            }

            var hash = Sha512(password, salt, spinCount);
            return string.Equals(Convert.ToBase64String(hash), hashBase64.Trim(), StringComparison.Ordinal);
        }

        private static byte[] Sha512(string password, byte[] salt, int spinCount)
        {
            using var sha = SHA512.Create();
            var text = Encoding.Unicode.GetBytes(password ?? string.Empty);
            var hash = sha.ComputeHash(salt.Concat(text).ToArray());
            for (var spin = 0; spin < spinCount; spin++)
                hash = sha.ComputeHash(BitConverter.GetBytes(spin).Concat(hash).ToArray());

            return hash;
        }
    }
}
