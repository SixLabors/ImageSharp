// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Evenly pads an image to fit the new dimensions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The source image to pad.</param>
        /// <param name="width">The new width.</param>
        /// <param name="height">The new height.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Pad<TPixel>(this IImageProcessingContext<TPixel> source, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            ResizeOptions options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.BoxPad,
                Sampler = new NearestNeighborResampler()
            };

            return Resize(source, options);
        }
    }
}