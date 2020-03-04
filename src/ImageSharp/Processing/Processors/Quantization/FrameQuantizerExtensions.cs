// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Contains extension methods for frame quantizers.
    /// </summary>
    public static class FrameQuantizerExtensions
    {
        /// <summary>
        /// Quantizes an image frame and return the resulting output pixels.
        /// </summary>
        /// <typeparam name="TFrameQuantizer">The type of frame quantizer.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="quantizer">The frame </param>
        /// <param name="source">The source image frame to quantize.</param>
        /// <param name="bounds">The bounds within the frame to quantize.</param>
        /// <returns>
        /// A <see cref="QuantizedFrame{TPixel}"/> representing a quantized version of the source frame pixels.
        /// </returns>
        public static QuantizedFrame<TPixel> QuantizeFrame<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ImageFrame<TPixel> source,
            Rectangle bounds)
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            var interest = Rectangle.Intersect(source.Bounds(), bounds);

            // Collect the palette. Required before the second pass runs.
            ReadOnlyMemory<TPixel> palette = quantizer.BuildPalette(source, interest);
            MemoryAllocator memoryAllocator = quantizer.Configuration.MemoryAllocator;

            var quantizedFrame = new QuantizedFrame<TPixel>(memoryAllocator, interest.Width, interest.Height, palette);
            Memory<byte> output = quantizedFrame.GetWritablePixelMemory();

            if (quantizer.Options.Dither is null)
            {
                SecondPass(ref quantizer, source, interest, output, palette);
            }
            else
            {
                // We clone the image as we don't want to alter the original via error diffusion based dithering.
                using (ImageFrame<TPixel> clone = source.Clone())
                {
                    SecondPass(ref quantizer, clone, interest, output, palette);
                }
            }

            return quantizedFrame;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void SecondPass<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ImageFrame<TPixel> source,
            Rectangle bounds,
            Memory<byte> output,
            ReadOnlyMemory<TPixel> palette)
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IDither dither = quantizer.Options.Dither;

            if (dither is null)
            {
                var operation = new RowIntervalOperation<TFrameQuantizer, TPixel>(quantizer, source, output, bounds, palette);
                ParallelRowIterator.IterateRowIntervals(
                    quantizer.Configuration,
                    bounds,
                    in operation);

                return;
            }

            dither.ApplyQuantizationDither(ref quantizer, palette, source, output, bounds);
        }

        private readonly struct RowIntervalOperation<TFrameQuantizer, TPixel> : IRowIntervalOperation
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            private readonly TFrameQuantizer quantizer;
            private readonly ImageFrame<TPixel> source;
            private readonly Memory<byte> output;
            private readonly Rectangle bounds;
            private readonly ReadOnlyMemory<TPixel> palette;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                in TFrameQuantizer quantizer,
                ImageFrame<TPixel> source,
                Memory<byte> output,
                Rectangle bounds,
                ReadOnlyMemory<TPixel> palette)
            {
                this.quantizer = quantizer;
                this.source = source;
                this.output = output;
                this.bounds = bounds;
                this.palette = palette;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;
                Span<byte> outputSpan = this.output.Span;
                int width = this.bounds.Width;
                int offsetY = this.bounds.Top;
                int offsetX = this.bounds.Left;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);
                    int rowStart = (y - offsetY) * width;

                    // TODO: This can be a bulk operation.
                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        outputSpan[rowStart + x - offsetX] = this.quantizer.GetQuantizedColor(row[x], paletteSpan, out TPixel _);
                    }
                }
            }
        }
    }
}
