// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies a global histogram equalization to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GlobalHistogramEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public GlobalHistogramEqualizationProcessor(
            Configuration configuration,
            int luminanceLevels,
            bool clipHistogram,
            int clipLimit,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
        {
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            MemoryAllocator memoryAllocator = this.Configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            using IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean);

            // Build the histogram of the grayscale levels
            var grayscaleOperation = new GrayscaleLevelsRowOperation(interest, histogramBuffer, source, this.LuminanceLevels);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                interest,
                in grayscaleOperation);

            Span<int> histogram = histogramBuffer.GetSpan();
            if (this.ClipHistogramEnabled)
            {
                this.ClipHistogram(histogram, this.ClipLimit);
            }

            using IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean);

            // Calculate the cumulative distribution function, which will map each input pixel to a new value.
            int cdfMin = this.CalculateCdf(
                ref MemoryMarshal.GetReference(cdfBuffer.GetSpan()),
                ref MemoryMarshal.GetReference(histogram),
                histogram.Length - 1);

            float numberOfPixelsMinusCdfMin = numberOfPixels - cdfMin;

            // Apply the cdf to each pixel of the image
            var cdfOperation = new CdfApplicationRowOperation(interest, cdfBuffer, source, this.LuminanceLevels, numberOfPixelsMinusCdfMin);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                interest,
                in cdfOperation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the grayscale levels logic for <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct GrayscaleLevelsRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly IMemoryOwner<int> histogramBuffer;
            private readonly ImageFrame<TPixel> source;
            private readonly int luminanceLevels;

            [MethodImpl(InliningOptions.ShortMethod)]
            public GrayscaleLevelsRowOperation(
                Rectangle bounds,
                IMemoryOwner<int> histogramBuffer,
                ImageFrame<TPixel> source,
                int luminanceLevels)
            {
                this.bounds = bounds;
                this.histogramBuffer = histogramBuffer;
                this.source = source;
                this.luminanceLevels = luminanceLevels;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                ref int histogramBase = ref MemoryMarshal.GetReference(this.histogramBuffer.GetSpan());
                ref TPixel pixelBase = ref MemoryMarshal.GetReference(this.source.GetPixelRowSpan(y));

                for (int x = 0; x < this.bounds.Width; x++)
                {
                    int luminance = GetLuminance(Unsafe.Add(ref pixelBase, x), this.luminanceLevels);
                    Unsafe.Add(ref histogramBase, luminance)++;
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the cdf application levels logic for <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/>.
        /// </summary>
        private readonly struct CdfApplicationRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly IMemoryOwner<int> cdfBuffer;
            private readonly ImageFrame<TPixel> source;
            private readonly int luminanceLevels;
            private readonly float numberOfPixelsMinusCdfMin;

            [MethodImpl(InliningOptions.ShortMethod)]
            public CdfApplicationRowOperation(
                Rectangle bounds,
                IMemoryOwner<int> cdfBuffer,
                ImageFrame<TPixel> source,
                int luminanceLevels,
                float numberOfPixelsMinusCdfMin)
            {
                this.bounds = bounds;
                this.cdfBuffer = cdfBuffer;
                this.source = source;
                this.luminanceLevels = luminanceLevels;
                this.numberOfPixelsMinusCdfMin = numberOfPixelsMinusCdfMin;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                ref int cdfBase = ref MemoryMarshal.GetReference(this.cdfBuffer.GetSpan());
                ref TPixel pixelBase = ref MemoryMarshal.GetReference(this.source.GetPixelRowSpan(y));

                for (int x = 0; x < this.bounds.Width; x++)
                {
                    ref TPixel pixel = ref Unsafe.Add(ref pixelBase, x);
                    int luminance = GetLuminance(pixel, this.luminanceLevels);
                    float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / this.numberOfPixelsMinusCdfMin;
                    pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                }
            }
        }
    }
}
