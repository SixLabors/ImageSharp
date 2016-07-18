// <copyright file="IQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    /// <summary>
    /// Provides methods for allowing quantization of images pixels.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public interface IQuantizer<T, TP> : IQuantizer
        where T : IPackedVector<TP>, new()
        where TP : struct
    {
        /// <summary>
        /// Quantize an image and return the resulting output pixels.
        /// </summary>
        /// <param name="image">The image to quantize.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>
        /// A <see cref="T:QuantizedImage"/> representing a quantized version of the image pixels.
        /// </returns>
        QuantizedImage<T, TP> Quantize(ImageBase<T, TP> image, int maxColors);
    }

    /// <summary>
    /// Provides methods for allowing quantization of images pixels.
    /// </summary>
    public interface IQuantizer
    {
        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        byte Threshold { get; set; }
    }
}
