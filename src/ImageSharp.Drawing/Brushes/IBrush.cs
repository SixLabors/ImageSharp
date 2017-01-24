// <copyright file="IBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;

    using Processors;

    /// <summary>
    /// Brush represents a logical configuration of a brush which can be used to source pixel colors
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <remarks>
    /// A brush is a simple class that will return an <see cref="BrushApplicator{TColor}" /> that will perform the
    /// logic for converting a pixel location to a <typeparamref name="TColor"/>.
    /// </remarks>
    public interface IBrush<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Creates the applicator for this brush.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        /// <param name="region">The region the brush will be applied to.</param>
        /// <returns>
        /// The brush applicator for this brush
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would usually be the
        /// bounding box of the shape not necessarily the bounds of the whole image
        /// </remarks>
        BrushApplicator<TColor> CreateApplicator(PixelAccessor<TColor> pixelSource, RectangleF region);
    }
}