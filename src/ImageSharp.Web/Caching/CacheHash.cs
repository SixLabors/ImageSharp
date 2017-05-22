// <copyright file="CacheHash.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System.Security.Cryptography;
    using System.Text;

    using ImageSharp.Web.Helpers;

    /// <summary>
    /// Creates hashed keys for the given inputs.
    /// Hashed keys are the result of Base32 encoding the SHA256 computation of the input value.
    /// This helps ensure compressable values with low collision rates.
    /// </summary>
    internal static class CacheHash
    {
        /// <summary>
        /// The Base32 Alphabet
        /// </summary>
        private static readonly char[] Base32Table =
        {
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
            'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '2', '3', '4', '5', '6', '7'
        };

        /// <summary>
        /// Create a cache key based on the given string.
        /// </summary>
        /// <param name="data">The data to create a key for.</param>
        /// <param name="configuration">The library configuration.</param>
        /// <returns>The hashed <see cref="string"/></returns>
        public static string Create(string data, Configuration configuration) => ComputeHash(data, configuration);

        /// <summary>
        /// Computes a hashed value for the given uri string.
        /// </summary>
        /// <param name="uri">The uri to hash.</param>
        /// <param name="configuration">The library configuration.</param>
        /// <returns>The hashed <see cref="string"/></returns>
        public static string ComputeHash(string uri, Configuration configuration)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return $"{Encode(hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(uri)))}.{FormatHelpers.GetExtensionOrDefault(configuration, uri)}";
            }
        }

        /// <summary>
        /// Returns a Base32 encoded string representation of the given hash.
        /// <see href="https://tools.ietf.org/html/rfc4648#section-6"/>
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>The Base32 encoded <see cref="string"/></returns>
        private static string Encode(byte[] hash)
        {
            // TODO: Write unit tests for this using http://www.simplycalc.com/base32-encode.php as a basis.
            const char Pad = '=';
            char[] b32 = Base32Table;
            int length = hash.Length;
            char[] result = new char[length + 7];
            int i;

            for (i = 0; i <= length - 5; i += 5)
            {
                result[i] = b32[hash[i] >> 3];
                result[i + 1] = b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)];
                result[i + 2] = b32[(hash[i + 1] & 0x3E) >> 1];
                result[i + 3] = b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)];
                result[i + 4] = b32[((hash[i + 2] & 0x0F) << 1) | (hash[i + 3] >> 7)];
                result[i + 5] = b32[(hash[i + 3] & 0x7C) >> 2];
                result[i + 6] = b32[((hash[i + 3] & 0x03) << 3) | ((hash[i + 4] & 0xE0) >> 5)];
                result[i + 7] = b32[hash[i + 4] & 0x1F];
            }

            switch (length % 5)
            {
                case 4:
                    result[i + 1] = b32[hash[i] >> 3];
                    result[i + 2] = b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)];
                    result[i + 3] = b32[(hash[i + 1] & 0x3E) >> 1];
                    result[i + 4] = b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)];
                    result[i + 5] = b32[((hash[i + 2] & 0x0F) << 1) | (hash[i + 3] >> 7)];
                    result[i + 6] = b32[(hash[i + 3] & 0x7C) >> 2];
                    result[i + 7] = b32[(hash[i + 3] & 0x03) << 3];
                    result[i + 8] = Pad;
                    break;
                case 3:
                    result[i + 1] = b32[hash[i] >> 3];
                    result[i + 2] = b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)];
                    result[i + 3] = b32[(hash[i + 1] & 0x3E) >> 1];
                    result[i + 4] = b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)];
                    result[i + 5] = b32[(hash[i + 2] & 0x0F) << 1];
                    result[i + 6] = Pad;
                    result[i + 7] = Pad;
                    result[i + 8] = Pad;
                    break;
                case 2:
                    result[i + 1] = b32[hash[i] >> 3];
                    result[i + 2] = b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)];
                    result[i + 3] = b32[(hash[i + 1] & 0x3E) >> 1];
                    result[i + 4] = b32[(hash[i + 1] & 0x01) << 4];
                    result[i + 5] = Pad;
                    result[i + 6] = Pad;
                    result[i + 7] = Pad;
                    result[i + 8] = Pad;
                    break;
                case 1:
                    result[i + 1] = b32[hash[i] >> 3];
                    result[i + 2] = b32[(hash[i] & 7) << 2];
                    result[i + 3] = Pad;
                    result[i + 4] = Pad;
                    result[i + 5] = Pad;
                    result[i + 6] = Pad;
                    result[i + 7] = Pad;
                    result[i + 8] = Pad;
                    break;
            }

            return new string(result);
        }
    }
}