using System;
using System.Linq;

namespace AzDocs.RepositoryProvisioner.Helpers
{
    public static class PasswordHelper
    {
        private static readonly char[] Punctuations = "!@$&?".ToCharArray();
        private static readonly char[] AlphaCharsCaps = "ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] AlphaCharsLower = "abcdefghijkmnpqrstuvwxyz".ToCharArray();

        /// <summary>
        /// Generate a random password of a fixed length (range from 5 to 128 characters) containing numbers, caps, smallcaps and special chars
        /// </summary>
        /// <param name="length"></param>
        /// <param name="numberOfNonAlphanumericCharacters"></param>
        /// <returns>A random password</returns>
        public static string Generate(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 5 || length > 128)
            {
                throw new ArgumentException(nameof(length));
            }

            if (numberOfNonAlphanumericCharacters > length - 4 || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException(nameof(numberOfNonAlphanumericCharacters));
            }

            var rnd = new Random();
            var numNumChars = rnd.Next(1, length - numberOfNonAlphanumericCharacters - 3);
            var numAlphaCharsCaps = rnd.Next(1, length - numberOfNonAlphanumericCharacters - numNumChars - 2);
            var numAlphaCharsLower = length - numberOfNonAlphanumericCharacters - numNumChars - numAlphaCharsCaps;

            var pwd = "";
            for (int i = 0; i < numNumChars; i++)
                pwd += (char)rnd.Next(48, 57);
            for (int i = 0; i < numAlphaCharsCaps; i++)
                pwd += AlphaCharsCaps[rnd.Next(0, AlphaCharsCaps.Length)];
            for (int i = 0; i < numAlphaCharsLower; i++)
                pwd += AlphaCharsLower[rnd.Next(0, AlphaCharsLower.Length)];
            for (int i = 0; i < numberOfNonAlphanumericCharacters; i++)
                pwd += Punctuations[rnd.Next(0, Punctuations.Length)];

            var shuffledPwd = string.Join("", pwd.OrderBy(c => rnd.Next()));
            return shuffledPwd;
        }
    }
}
