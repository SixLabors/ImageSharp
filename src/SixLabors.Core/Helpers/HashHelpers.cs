// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors
{
    /// <summary>
    /// Provides a set of helpers for combining object hashes.
    /// </summary>
    internal static class HashHelpers
    {
        /// <summary>
        /// Combines the two specified hash codes.
        /// </summary>
        /// <param name="h1">Hash code one</param>
        /// <param name="h2">Hash code two</param>
        /// <returns>Returns a hash code for the provided hash codes.</returns>
        public static int Combine(int h1, int h2)
        {
            // Lifted from coreFX repo

            unchecked
            {
                // RyuJIT optimizes this to use the ROL instruction
                // Related GitHub pull request: dotnet/coreclr#1830
                uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
                return ((int)rol5 + h1) ^ h2;
            }
        }

        /// <summary>
        /// Combines the three specified hash codes.
        /// </summary>
        /// <param name="h1">The first </param>
        /// <param name="h2">Hash code two</param>
        /// <param name="h3">Hash code three</param>
        /// <returns>Returns a hash code for the provided hash codes.</returns>
        public static int Combine(int h1, int h2, int h3)
        {
            int hash = Combine(h1, h2);

            hash = Combine(hash, h3);

            return hash;
        }

        /// <summary>
        /// Combines the four specified hash codes.
        /// </summary>
        /// <param name="h1">The first </param>
        /// <param name="h2">Hash code two</param>
        /// <param name="h3">Hash code three</param>
        /// <param name="h4">Hash code four</param>
        /// <returns>Returns a hash code for the provided hash codes.</returns>
        public static int Combine(int h1, int h2, int h3, int h4)
        {
            int hash = Combine(h1, h2);

            hash = Combine(hash, h3);

            return Combine(hash, h4);
        }
    }
}