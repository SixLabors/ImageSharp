// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Common constants used throughout the project
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// The epsilon value for comparing floating point numbers.
        /// </summary>
        public static readonly float Epsilon = 0.001F;

        /// <summary>
        /// The epsilon squared value for comparing floating point numbers.
        /// </summary>
        public static readonly float EpsilonSquared = Epsilon * Epsilon;
    }
}