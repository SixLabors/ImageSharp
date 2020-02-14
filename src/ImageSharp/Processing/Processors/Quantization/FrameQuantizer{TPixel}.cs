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
    /// The base class for all <see cref="IFrameQuantizer{TPixel}"/> implementations
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public abstract class FrameQuantizer<TPixel> : IFrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        private EuclideanPixelMap<TPixel> pixelMap;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="quantizer">The quantizer.</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once.
        /// </param>
        /// <remarks>
        /// If you construct this class with a <value>true</value> for <paramref name="singlePass"/>, then the code will
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Rectangle, Memory{byte}, ReadOnlyMemory{TPixel})"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, Rectangle)"/>.
        /// </remarks>
        protected FrameQuantizer(Configuration configuration, IQuantizer quantizer, bool singlePass)
        {
            Guard.NotNull(quantizer, nameof(quantizer));

            this.Configuration = configuration;
            this.Dither = quantizer.Dither;
            this.DoDither = this.Dither != null;
            this.singlePass = singlePass;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="diffuser">The diffuser</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a <value>true</value> for <paramref name="singlePass"/>, then the code will
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Rectangle, Memory{byte}, ReadOnlyMemory{TPixel})"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, Rectangle)"/>.
        /// </remarks>
        protected FrameQuantizer(Configuration configuration, IDither diffuser, bool singlePass)
        {
            this.Configuration = configuration;
            this.Dither = diffuser;
            this.DoDither = this.Dither != null;
            this.singlePass = singlePass;
        }

        /// <inheritdoc />
        public IDither Dither { get; }

        /// <inheritdoc />
        public bool DoDither { get; }

        /// <summary>
        /// Gets the configuration which allows altering default behaviour or extending the library.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public IQuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> image, Rectangle bounds)
        {
            Guard.NotNull(image, nameof(image));
            var interest = Rectangle.Intersect(image.Bounds(), bounds);

            // Call the FirstPass function if not a single pass algorithm.
            // For something like an Octree quantizer, this will run through
            // all image pixels, build a data structure, and create a palette.
            if (!this.singlePass)
            {
                this.FirstPass(image, interest);
            }

            // Collect the palette. Required before the second pass runs.
            ReadOnlyMemory<TPixel> palette = this.GenerateQuantizedPalette();
            MemoryAllocator memoryAllocator = this.Configuration.MemoryAllocator;
            this.pixelMap = new EuclideanPixelMap<TPixel>(palette);

            var quantizedFrame = new QuantizedFrame<TPixel>(memoryAllocator, interest.Width, interest.Height, palette);

            Memory<byte> output = quantizedFrame.GetWritablePixelMemory();
            if (this.DoDither)
            {
                // We clone the image as we don't want to alter the original via error diffusion based dithering.
                using (ImageFrame<TPixel> clone = image.Clone())
                {
                    this.SecondPass(clone, interest, output, palette);
                }
            }
            else
            {
                this.SecondPass(image, interest, output, palette);
            }

            return quantizedFrame;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed and unmanaged objects.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image to create the palette.
        /// </summary>
        /// <param name="source">The source data.</param>
        /// <param name="bounds">The bounds within the source image to quantize.</param>
        protected virtual void FirstPass(ImageFrame<TPixel> source, Rectangle bounds)
        {
        }

        /// <summary>
        /// Returns the index and color from the quantized palette corresponding to the give to the given color.
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <param name="palette">The output color palette.</param>
        /// <param name="match">The matched color.</param>
        /// <returns>The <see cref="byte"/> index.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        protected virtual byte GetQuantizedColor(TPixel color, ReadOnlySpan<TPixel> palette, out TPixel match)
            => this.pixelMap.GetClosestColor(color, out match);

        /// <summary>
        /// Generates the palette for the quantized image.
        /// </summary>
        /// <returns>
        /// <see cref="ReadOnlyMemory{TPixel}"/>
        /// </returns>
        protected abstract ReadOnlyMemory<TPixel> GenerateQuantizedPalette();

        /// <summary>
        /// Execute a second pass through the image to assign the pixels to a palette entry.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="bounds">The bounds within the source image to quantize.</param>
        /// <param name="output">The output pixel array.</param>
        /// <param name="palette">The output color palette.</param>
        protected virtual void SecondPass(
            ImageFrame<TPixel> source,
            Rectangle bounds,
            Memory<byte> output,
            ReadOnlyMemory<TPixel> palette)
        {
            ReadOnlySpan<TPixel> paletteSpan = palette.Span;
            if (!this.DoDither)
            {
                var operation = new RowIntervalOperation(source, output, bounds, this, palette);
                ParallelRowIterator.IterateRows(
                    this.Configuration,
                    bounds,
                    in operation);

                return;
            }

            // Error diffusion.
            // The difference between the source and transformed color is spread to neighboring pixels.
            // TODO: Investigate parallel strategy.
            Span<byte> outputSpan = output.Span;

            int bitDepth = ImageMaths.GetBitsNeededForColorDepth(paletteSpan.Length);
            if (this.Dither.DitherType == DitherType.ErrorDiffusion)
            {
                int width = bounds.Width;
                int offsetX = bounds.Left;
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y);
                    int offset = y * width;

                    for (int x = bounds.Left; x < bounds.Right; x++)
                    {
                        TPixel sourcePixel = row[x];
                        outputSpan[offset + x - offsetX] = this.GetQuantizedColor(sourcePixel, paletteSpan, out TPixel transformed);
                        this.Dither.Dither(source, bounds, sourcePixel, transformed, x, y, bitDepth);
                    }
                }

                return;
            }

            // Ordered dithering. We are only operating on a single pixel so we can work in parallel.
            var ditherOperation = new DitherRowIntervalOperation(source, output, bounds, this, palette, bitDepth);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                bounds,
                in ditherOperation);
        }

        private readonly struct RowIntervalOperation : IRowIntervalOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly Memory<byte> output;
            private readonly Rectangle bounds;
            private readonly FrameQuantizer<TPixel> quantizer;
            private readonly ReadOnlyMemory<TPixel> palette;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                ImageFrame<TPixel> source,
                Memory<byte> output,
                Rectangle bounds,
                FrameQuantizer<TPixel> quantizer,
                ReadOnlyMemory<TPixel> palette)
            {
                this.source = source;
                this.output = output;
                this.bounds = bounds;
                this.quantizer = quantizer;
                this.palette = palette;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;
                Span<byte> outputSpan = this.output.Span;
                int width = this.bounds.Width;
                int offsetX = this.bounds.Left;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);
                    int offset = y * width;

                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        outputSpan[offset + x - offsetX] = this.quantizer.GetQuantizedColor(row[x], paletteSpan, out TPixel _);
                    }
                }
            }
        }

        private readonly struct DitherRowIntervalOperation : IRowIntervalOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly Memory<byte> output;
            private readonly Rectangle bounds;
            private readonly FrameQuantizer<TPixel> quantizer;
            private readonly ReadOnlyMemory<TPixel> palette;
            private readonly int bitDepth;

            [MethodImpl(InliningOptions.ShortMethod)]
            public DitherRowIntervalOperation(
                ImageFrame<TPixel> source,
                Memory<byte> output,
                Rectangle bounds,
                FrameQuantizer<TPixel> quantizer,
                ReadOnlyMemory<TPixel> palette,
                int bitDepth)
            {
                this.source = source;
                this.output = output;
                this.bounds = bounds;
                this.quantizer = quantizer;
                this.palette = palette;
                this.bitDepth = bitDepth;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;
                Span<byte> outputSpan = this.output.Span;
                int width = this.bounds.Width;
                IDither dither = this.quantizer.Dither;
                TPixel transformed = default;
                int offsetX = this.bounds.Left;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);
                    int offset = y * width;
                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        TPixel dithered = dither.Dither(this.source, this.bounds, row[x], transformed, x, y, this.bitDepth);
                        outputSpan[offset + x - offsetX] = this.quantizer.GetQuantizedColor(dithered, paletteSpan, out TPixel _);
                    }
                }
            }
        }
    }
}
