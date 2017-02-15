// <copyright file="Ordered.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering.Ordered
{
    using System;

    /// <summary>
    /// Applies error diffusion based dithering using the 4x4 ordered dithering matrix.
    /// <see href="https://en.wikipedia.org/wiki/Ordered_dithering"/>
    /// </summary>
    public class Ordered : IOrderedDither
    {
        /// <summary>
        /// The threshold matrix.
        /// This is calculated by multiplying each value in the original matrix by 16
        /// </summary>
        private static readonly byte[][] ThresholdMatrix =
        {
           new byte[] { 0, 128, 32, 160 },
           new byte[] { 192, 64, 224, 96 },
           new byte[] { 48, 176, 16, 144 },
           new byte[] { 240, 112, 208, 80 }
        };

        /// <inheritdoc />
        public byte[][] Matrix { get; } = ThresholdMatrix;

        /// <inheritdoc />
        public void Dither<TColor>(PixelAccessor<TColor> pixels, TColor source, TColor upper, TColor lower, byte[] bytes, int index, int x, int y, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            source.ToXyzwBytes(bytes, 0);
            pixels[x, y] = ThresholdMatrix[x % 3][y % 3] >= bytes[index] ? lower : upper;
        }
    }
}