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
        /// <param name="quantizer">The quantizer</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a <value>true</value> for <paramref name="singlePass"/>, then the code will
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Span{byte}, ReadOnlySpan{TPixel},  int, int)"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, int, int)"/>.
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
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Span{byte}, ReadOnlySpan{TPixel},  int, int)"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, int, int)"/>.
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
        public IQuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> image)
        {
            Guard.NotNull(image, nameof(image));

            // Get the size of the source image
            int height = image.Height;
            int width = image.Width;

            // Call the FirstPass function if not a single pass algorithm.
            // For something like an Octree quantizer, this will run through
            // all image pixels, build a data structure, and create a palette.
            if (!this.singlePass)
            {
                this.FirstPass(image, width, height);
            }

            // Collect the palette. Required before the second pass runs.
            ReadOnlyMemory<TPixel> palette = this.GetPalette();
            MemoryAllocator memoryAllocator = this.Configuration.MemoryAllocator;
            this.pixelMap = new EuclideanPixelMap<TPixel>(palette);

            var quantizedFrame = new QuantizedFrame<TPixel>(memoryAllocator, width, height, palette);

            Span<byte> pixelSpan = quantizedFrame.GetWritablePixelSpan();
            if (this.DoDither)
            {
                // We clone the image as we don't want to alter the original via dithering.
                using (ImageFrame<TPixel> clone = image.Clone())
                {
                    this.SecondPass(clone, pixelSpan, palette.Span, width, height);
                }
            }
            else
            {
                this.SecondPass(image, pixelSpan, palette.Span, width, height);
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
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
        }

        /// <summary>
        /// Returns the index and color from the quantized palette corresponding to the give to the given color.
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <param name="match">The matched color.</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        protected virtual byte GetQuantizedColor(TPixel color, out TPixel match)
            => this.pixelMap.GetClosestColor(color, out match);

        /// <summary>
        /// Retrieve the palette for the quantized image.
        /// </summary>
        /// <returns>
        /// <see cref="ReadOnlyMemory{TPixel}"/>
        /// </returns>
        protected abstract ReadOnlyMemory<TPixel> GetPalette();

        /// <summary>
        /// Execute a second pass through the image to assign the pixels to a palette entry.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="output">The output pixel array.</param>
        /// <param name="palette">The output color palette.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void SecondPass(
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
    }
}
