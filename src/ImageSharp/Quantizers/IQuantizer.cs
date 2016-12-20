// <copyright file="IQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;

    /// <summary>
    /// Provides methods for allowing quantization of images pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IQuantizer<TColor> : IQuantizer
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Quantize an image and return the resulting output pixels.
        /// </summary>
        /// <param name="image">The image to quantize.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>
        /// A <see cref="T:QuantizedImage"/> representing a quantized version of the image pixels.
        /// </returns>
        QuantizedImage<TColor> Quantize(ImageBase<TColor> image, int maxColors);
    }

    /// <summary>
    /// Provides methods for allowing quantization of images pixels.
    /// </summary>
    public interface IQuantizer
    {
    }
}
