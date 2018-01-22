// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Helper methods fro acccess pixel accessors
    /// </summary>
    internal static class PixelAccessorExtensions
    {
        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="frame">The frame.</param>
        /// <returns>
        /// The <see cref="PixelAccessor{TPixel}" />
        /// </returns>
        internal static PixelAccessor<TPixel> Lock<TPixel>(this IPixelSource<TPixel> frame)
        where TPixel : struct, IPixel<TPixel>
        {
            return new PixelAccessor<TPixel>(frame);
        }

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="image">The image.</param>
        /// <returns>
        /// The <see cref="PixelAccessor{TPixel}" />
        /// </returns>
        internal static PixelAccessor<TPixel> Lock<TPixel>(this Image<TPixel> image)
        where TPixel : struct, IPixel<TPixel>
        {
            return image.Frames.RootFrame.Lock();
        }
    }
}