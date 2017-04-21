// <copyright file="IPen.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using ImageSharp.PixelFormats;
    using Processors;

    /// <summary>
    /// Interface representing a Pen
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public interface IPen<TPixel>
            where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Creates the applicator for applying this pen to an Image
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        /// <param name="region">The region the pen will be applied to.</param>
        /// <returns>
        /// Returns a the applicator for the pen.
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would usually be the bounding box of the shape not necessarily the shape of the whole image.
        /// </remarks>
        PenApplicator<TPixel> CreateApplicator(PixelAccessor<TPixel> pixelSource, RectangleF region);
    }
}
