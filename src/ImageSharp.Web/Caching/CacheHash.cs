// <copyright file="CacheHash.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Creates hashed keys for the given inputs.
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
        /// Create a cache key based on the given byte array.
        /// </summary>
        /// <param name="data">The data to create a key for.</param>
        /// <returns>The hashed <see cref="string"/></returns>
        public static string Create(byte[] data) => ComputeHash(data);

        /// <summary>
        /// Create a cache key based on the given string.
        /// </summary>
        /// <param name="data">The data to create a key for.</param>
        /// <returns>The hashed <see cref="string"/></returns>
        public static string Create(string data) => Create(Encoding.UTF8.GetBytes(data));

        /// <summary>
        /// Computes a hashed value for the given byte array.
        /// </summary>
        /// <param name="data">The data to hash.</param>
        /// <returns>The hashed <see cref="string"/></returns>
        private static string ComputeHash(byte[] data)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return Encode(hashAlgorithm.ComputeHash(data));
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
            char[] b32 = Base32Table;
            var sb = new StringBuilder();
            int length = hash.Length;
            int i;

            for (i = 0; i <= length - 5; i += 5)
            {
                sb.Append(b32[hash[i] >> 3]);
                sb.Append(b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)]);
                sb.Append(b32[(hash[i + 1] & 0x3E) >> 1]);
                sb.Append(b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)]);
                sb.Append(b32[((hash[i + 2] & 0x0F) << 1) | (hash[i + 3] >> 7)]);
                sb.Append(b32[(hash[i + 3] & 0x7C) >> 2]);
                sb.Append(b32[((hash[i + 3] & 0x03) << 3) | ((hash[i + 4] & 0xE0) >> 5)]);
                sb.Append(b32[hash[i + 4] & 0x1F]);
            }

            switch (length % 5)
            {
                case 4:
                    sb.Append(b32[hash[i] >> 3]);
                    sb.Append(b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)]);
                    sb.Append(b32[(hash[i + 1] & 0x3E) >> 1]);
                    sb.Append(b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)]);
                    sb.Append(b32[((hash[i + 2] & 0x0F) << 1) | (hash[i + 3] >> 7)]);
                    sb.Append(b32[(hash[i + 3] & 0x7C) >> 2]);
                    sb.Append(b32[(hash[i + 3] & 0x03) << 3]);
                    sb.Append('=');
                    break;
                case 3:
                    sb.Append(b32[hash[i] >> 3]);
                    sb.Append(b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)]);
                    sb.Append(b32[(hash[i + 1] & 0x3E) >> 1]);
                    sb.Append(b32[((hash[i + 1] & 0x01) << 4) | (hash[i + 2] >> 4)]);
                    sb.Append(b32[(hash[i + 2] & 0x0F) << 1]);
                    sb.Append("===");
                    break;
                case 2:
                    sb.Append(b32[hash[i] >> 3]);
                    sb.Append(b32[((hash[i] & 0x07) << 2) | (hash[i + 1] >> 6)]);
                    sb.Append(b32[(hash[i + 1] & 0x3E) >> 1]);
                    sb.Append(b32[(hash[i + 1] & 0x01) << 4]);
                    sb.Append("====");
                    break;
                case 1:
                    sb.Append(b32[hash[i] >> 3]);
                    sb.Append(b32[(hash[i] & 7) << 2]);
                    sb.Append("======");
                    break;
            }

            return sb.ToString();
        }
    }
}