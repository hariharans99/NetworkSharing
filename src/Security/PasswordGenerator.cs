using System;
using System.Security.Cryptography;
using System.Text;

namespace NetworkSharing.Security
{
    public static class PasswordGenerator
    {
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // removed I, O for clarity
        private const string Lower = "abcdefghijkmnpqrstuvwxyz"; // removed l, o for clarity
        private const string Digits = "23456789";               // removed 0, 1 for clarity
        private const string Special = "!@#$%^*_+-=";

        public static string Generate(int length = 14)
        {
            if (length < 8) length = 8;
            if (length > 64) length = 64;

            string allChars = Upper + Lower + Digits + Special;
            byte[] randomBytes = new byte[length];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            char[] password = new char[length];

            // Guarantee diversity
            password[0] = Upper[randomBytes[0] % Upper.Length];
            password[1] = Lower[randomBytes[1] % Lower.Length];
            password[2] = Digits[randomBytes[2] % Digits.Length];
            password[3] = Special[randomBytes[3] % Special.Length];

            for (int i = 4; i < length; i++)
            {
                password[i] = allChars[randomBytes[i] % allChars.Length];
            }

            // Shuffle array using Fisher-Yates with RNG
            byte[] shuffleBytes = new byte[length];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(shuffleBytes);
            }

            for (int i = length - 1; i > 0; i--)
            {
                int j = shuffleBytes[i] % (i + 1);
                char temp = password[i];
                password[i] = password[j];
                password[j] = temp;
            }

            return new string(password);
        }
    }
}
