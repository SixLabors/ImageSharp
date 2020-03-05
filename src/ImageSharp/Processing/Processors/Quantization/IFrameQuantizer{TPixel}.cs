// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Provides methods to allow the execution of the quantization process on an image frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IFrameQuantizer<TPixel> : IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        Configuration Configuration { get; }

        /// <summary>
        /// Gets the quantizer options defining quantization rules.
        /// </summary>
        QuantizerOptions Options { get; }

        /// <summary>
        /// Gets the quantized color palette.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The palette has not been built via <see cref="BuildPalette(ImageFrame{TPixel}, Rectangle)"/>.
        /// </exception>
        ReadOnlyMemory<TPixel> Palette { get; }

        /// <summary>
        /// Builds the quantized palette from the given image frame and bounds.
        /// </summary>
        /// <param name="source">The source image frame.</param>
        /// <param name="bounds">The region of interest bounds.</param>
        void BuildPalette(ImageFrame<TPixel> source, Rectangle bounds);

        /// <summary>
        /// Quantizes an image frame and return the resulting output pixels.
        /// </summary>
        /// <param name="source">The source image frame to quantize.</param>
        /// <param name="bounds">The bounds within the frame to quantize.</param>
        /// <returns>
        /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
        /// </returns>
        IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds);

        /// <summary>
        /// Returns the index and color from the quantized palette corresponding to the given color.
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <param name="match">The matched color.</param>
        /// <returns>The <see cref="byte"/> index.</returns>
        byte GetQuantizedColor(TPixel color, out TPixel match);

        // TODO: Enable bulk operations.
        // void GetQuantizedColors(ReadOnlySpan<TPixel> colors, ReadOnlySpan<TPixel> palette, Span<byte> indices, Span<TPixel> matches);
    }
}
