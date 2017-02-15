// <copyright file="IErrorDiffuser.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using System;

    /// <summary>
    /// Encapsulates properties and methods required to perfom diffused error dithering on an image.
    /// </summary>
    public interface IErrorDiffuser
    {
        /// <summary>
        /// Gets the dithering matrix
        /// </summary>
        byte[][] Matrix { get; }

        /// <summary>
        /// Transforms the image applying the dither matrix. This method alters the input pixels array
        /// </summary>
        /// <param name="pixels">The pixel accessor </param>
        /// <param name="source">The source pixel</param>
        /// <param name="transformed">The transformed pixel</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        void Dither<TColor>(PixelAccessor<TColor> pixels, TColor source, TColor transformed, int x, int y, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>;
    }
}
