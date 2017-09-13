// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors
{
    /// <summary>
    /// Lifted from coreFX repo
    /// </summary>
    internal static class HashHelpers
    {
        /// <summary>
        /// Combines the two specified hash codes.
        /// </summary>
        /// <param name="h1">Hash code one</param>
        /// <param name="h2">Hash code two</param>
        /// <returns>Returns a hash code for the two specified has codes.</returns>
        public static int Combine(int h1, int h2)
        {
            unchecked
            {
                // RyuJIT optimizes this to use the ROL instruction
                // Related GitHub pull request: dotnet/coreclr#1830
                uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
                return ((int)rol5 + h1) ^ h2;
            }
        }
    }
}
