// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Quantization.Processors
{
    /// <summary>
    /// Enables the quantization of images to remove the number of colors used in the image palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class QuantizeProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The quantizer used to reduce the color palette</param>
        /// <param name="maxColors">The maximum number of colors to reduce the palette to</param>
        public QuantizeProcessor(IQuantizer<TPixel> quantizer, int maxColors)
        {
            Guard.NotNull(quantizer, nameof(quantizer));
            Guard.MustBeGreaterThan(maxColors, 0, nameof(maxColors));

            this.Quantizer = quantizer;
            this.MaxColors = maxColors;
        }

        /// <summary>
        /// Gets the quantizer
        /// </summary>
        public IQuantizer<TPixel> Quantizer { get; }

        /// <summary>
        /// Gets the maximum number of palette colors
        /// </summary>
        public int MaxColors { get; }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            QuantizedFrame<TPixel> quantized = this.Quantizer.Quantize(source, this.MaxColors);
            int paletteCount = quantized.Palette.Length - 1;

            using (Buffer2D<TPixel> pixels = source.MemoryManager.Allocate2D<TPixel>(quantized.Width, quantized.Height))
            {
                Parallel.For(
                    0,
                    pixels.Height,
                    configuration.ParallelOptions,
                    y =>
                        {
                            Span<TPixel> row = pixels.GetRowSpan(y);
                            int yy = y * pixels.Width;
                            for (int x = 0; x < pixels.Width; x++)
                            {
                                int i = x + yy;
                                TPixel color = quantized.Palette[Math.Min(paletteCount, quantized.Pixels[i])];
                                row[x] = color;
                            }
                        });

                Buffer2D<TPixel>.SwapContents(source.PixelBuffer, pixels);
            }
        }
    }
}