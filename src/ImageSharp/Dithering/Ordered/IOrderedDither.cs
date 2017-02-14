// <copyright file="IOrderedDither.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using System;

    /// <summary>
    /// Encapsulates properties and methods required to perfom ordered dithering on an image.
    /// </summary>
    public interface IOrderedDither
    {
        /// <summary>
        /// Gets the dithering matrix
        /// </summary>
        byte[,] Matrix { get; }

        /// <summary>
        /// Transforms the image applying the dither matrix. This method alters the input pixels array
        /// </summary>
        /// <param name="pixels">The pixel accessor </param>
        /// <param name="source">The source pixel</param>
        /// <param name="upper">The color to apply to the pixels above the threshold.</param>
        /// <param name="lower">The color to apply to the pixels below the threshold.</param>
        /// <param name="bytes">The byte array to pack/unpack to. Must have a length of 4. Bytes are unpacked to Xyzw order.</param>
        /// <param name="index">The component index to test the threshold against. Must range from 0 to 3.</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        void Dither<TColor>(PixelAccessor<TColor> pixels, TColor source, TColor upper, TColor lower, byte[] bytes, int index, int x, int y, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>;
    }
}
