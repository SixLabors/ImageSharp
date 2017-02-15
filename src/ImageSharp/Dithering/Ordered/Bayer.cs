// <copyright file="Bayer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering.Ordered
{
    using System;

    /// <summary>
    /// Applies error diffusion based dithering using the 4x4 Bayer dithering matrix.
    /// <see href="http://www.efg2.com/Lab/Library/ImageProcessing/DHALF.TXT"/>
    /// </summary>
    public class Bayer : IOrderedDither
    {
        /// <summary>
        /// The threshold matrix.
        /// This is calculated by multiplying each value in the original matrix by 16 and subtracting 1
        /// </summary>
        private static readonly byte[,] ThresholdMatrix =
        {
            { 15, 143, 47, 175 },
            { 207, 79, 239, 111 },
            { 63, 191, 31, 159 },
            { 255, 127, 223, 95 }
        };

        /// <inheritdoc />
        public byte[,] Matrix { get; } = ThresholdMatrix;

        /// <inheritdoc />
        public void Dither<TColor>(PixelAccessor<TColor> pixels, TColor source, TColor upper, TColor lower, byte[] bytes, int index, int x, int y, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            source.ToXyzwBytes(bytes, 0);
            pixels[x, y] = ThresholdMatrix[x % 3, y % 3] >= bytes[index] ? lower : upper;
        }
    }
}