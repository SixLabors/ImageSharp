// <copyright file="ImageMaths.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Provides common mathematical methods.
    /// </summary>
    internal static class ImageMaths
    {
        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        public static int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }
    }
}
