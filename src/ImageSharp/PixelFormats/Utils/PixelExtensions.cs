// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    /// <summary>
    /// Low-performance extension methods to help conversion syntax, suitable for testing purposes.
    /// </summary>
    internal static class PixelExtensions
    {
        /// <summary>
        /// Returns the result of <see cref="IPixel.ToRgba32"/> as a new <see cref="Rgba32"/> instance.
        /// </summary>
        public static Rgba32 ToRgba32<TPixel>(this TPixel pixel)
            where TPixel : struct, IPixel<TPixel>
        {
            Rgba32 result = default;
            pixel.ToRgba32(ref result);
            return result;
        }
    }
}