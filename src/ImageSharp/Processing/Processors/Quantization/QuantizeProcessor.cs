// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Enables the quantization of images to reduce the number of colors used in the image palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class QuantizeProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The quantizer used to reduce the color palette</param>
        public QuantizeProcessor(IQuantizer quantizer)
        {
            Guard.NotNull(quantizer, nameof(quantizer));
            this.Quantizer = quantizer;
        }

        /// <summary>
        /// Gets the quantizer
        /// </summary>
        public IQuantizer Quantizer { get; }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            IFrameQuantizer<TPixel> executor = this.Quantizer.CreateFrameQuantizer<TPixel>();
            using (QuantizedFrame<TPixel> quantized = executor.QuantizeFrame(source))
            {
                int paletteCount = quantized.Palette.Length - 1;

                // Not parallel to remove "quantized" closure allocation.
                // We can operate directly on the source here as we've already read it to get the
                // quantized result
                for (int y = 0; y < source.Height; y++)
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y);
                    ReadOnlySpan<byte> quantizedPixelSpan = quantized.GetPixelSpan();
                    int yy = y * source.Width;

                    for (int x = 0; x < source.Width; x++)
                    {
                        int i = x + yy;
                        row[x] = quantized.Palette[Math.Min(paletteCount, quantizedPixelSpan[i])];
                    }
                }
            }
        }
    }
}