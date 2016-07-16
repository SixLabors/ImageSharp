// <copyright file="IQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    /// <summary>
    /// Provides methods for allowing quantization of images pixels.
    /// </summary>
    public interface IQuantizer
    {
        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        byte Threshold { get; set; }

        /// <summary>
        /// Quantize an image and return the resulting output pixels.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="image">The image to quantize.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>
        /// A <see cref="T:QuantizedImage"/> representing a quantized version of the image pixels.
        /// </returns>
        QuantizedImage<T, TP> Quantize<T, TP>(ImageBase<T, TP> image, int maxColors)
            where T : IPackedVector<TP>, new()
            where TP : struct;
    }
}
