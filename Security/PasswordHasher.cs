using System.Security.Cryptography;
using System.Text;

namespace TaskManagerAPI.Security
{
    public static class PasswordHasher
    {
        private const string AlgorithmName = "pbkdf2-sha256";
        private const int CurrentIterations = 600_000;
        private const int MaxAcceptedIterations = 1_200_000;
        private const int SaltSize = 16;
        private const int DerivedKeySize = 32;
        private const int LegacySaltSize = 16;
        private const int LegacyHashSize = 32;
        private static readonly HashAlgorithmName Prf = HashAlgorithmName.SHA256;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var derivedKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, Prf, DerivedKeySize);

            return $"{AlgorithmName}${CurrentIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derivedKey)}";
        }

        public static bool Verify(string password, string storedHash, out bool needsRehash)
        {
            needsRehash = false;

            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            if (storedHash.Contains('$'))
            {
                return VerifyPbkdf2(password, storedHash, out needsRehash);
            }

            if (storedHash.Contains('.'))
            {
                return VerifyLegacy(password, storedHash, out needsRehash);
            }

            return false;
        }

        private static bool VerifyPbkdf2(string password, string storedHash, out bool needsRehash)
        {
            needsRehash = false;

            var segments = storedHash.Split('$');
            if (segments.Length != 4)
            {
                return false;
            }

            if (!string.Equals(segments[0], AlgorithmName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!int.TryParse(segments[1], out var iterations) || iterations <= 0)
            {
                return false;
            }

            if (iterations > MaxAcceptedIterations)
            {
                return false;
            }

            byte[] salt;
            byte[] expectedKey;
            try
            {
                salt = Convert.FromBase64String(segments[2]);
                expectedKey = Convert.FromBase64String(segments[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length != SaltSize || expectedKey.Length != DerivedKeySize)
            {
                return false;
            }

            var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Prf, DerivedKeySize);
            var isValid = CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);

            if (isValid && iterations < CurrentIterations)
            {
                needsRehash = true;
            }

            return isValid;
        }

        private static bool VerifyLegacy(string password, string storedHash, out bool needsRehash)
        {
            needsRehash = false;

            var parts = storedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[0]);
                expectedHash = Convert.FromBase64String(parts[1]);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length != LegacySaltSize || expectedHash.Length != LegacyHashSize)
            {
                return false;
            }

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var payload = new byte[salt.Length + passwordBytes.Length];

            Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, payload, salt.Length, passwordBytes.Length);

            var actualHash = SHA256.HashData(payload);
            var isValid = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);

            if (isValid)
            {
                needsRehash = true;
            }

            return isValid;
        }
    }
}
