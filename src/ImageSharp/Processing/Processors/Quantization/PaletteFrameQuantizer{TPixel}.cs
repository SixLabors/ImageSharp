// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PaletteFrameQuantizer<TPixel> : FrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The reduced image palette.
        /// </summary>
        private readonly ReadOnlyMemory<TPixel> palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="diffuser">The palette quantizer.</param>
        /// <param name="colors">An array of all colors in the palette.</param>
        public PaletteFrameQuantizer(Configuration configuration, IDither diffuser, ReadOnlyMemory<TPixel> colors)
            : base(configuration, diffuser, true) => this.palette = colors;

        /// <inheritdoc/>
        protected override void SecondPass(
            ImageFrame<TPixel> source,
            Span<byte> output,
            ReadOnlySpan<TPixel> palette,
            int width,
            int height)
        {
            Rectangle interest = source.Bounds();
            int bitDepth = ImageMaths.GetBitsNeededForColorDepth(palette.Length);

            if (!this.DoDither)
            {
                // TODO: This can be parallel.
                for (int y = interest.Top; y < interest.Bottom; y++)
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y);
                    int offset = y * width;

                    for (int x = interest.Left; x < interest.Right; x++)
                    {
                        output[offset + x] = this.GetQuantizedColor(row[x], out TPixel _);
                    }
                }

                return;
            }

            // Error diffusion. The difference between the source and transformed color
            // is spread to neighboring pixels.
            if (this.Dither.TransformColorBehavior == DitherTransformColorBehavior.PreOperation)
            {
                for (int y = interest.Top; y < interest.Bottom; y++)
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y);
                    int offset = y * width;

                    for (int x = interest.Left; x < interest.Right; x++)
                    {
                        TPixel sourcePixel = row[x];
                        output[offset + x] = this.GetQuantizedColor(sourcePixel, out TPixel transformed);
                        this.Dither.Dither(source, interest, sourcePixel, transformed, x, y, bitDepth);
                    }
                }

                return;
            }

            // TODO: This can be parallel.
            // Ordered dithering. We are only operating on a single pixel.
            for (int y = interest.Top; y < interest.Bottom; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                int offset = y * width;

                for (int x = interest.Left; x < interest.Right; x++)
                {
                    TPixel dithered = this.Dither.Dither(source, interest, row[x], default, x, y, bitDepth);
                    output[offset + x] = this.GetQuantizedColor(dithered, out TPixel _);
                }
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ReadOnlyMemory<TPixel> GetPalette() => this.palette;
    }
}
