// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

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
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);

        using IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean);

        // Build the histogram of the grayscale levels.
        GrayscaleLevelsRowOperation<TPixel> grayscaleOperation = new(this.Configuration, interest, histogramBuffer, source.PixelBuffer, this.LuminanceLevels);
        ParallelRowIterator.IterateRows<GrayscaleLevelsRowOperation<TPixel>, Vector4>(
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
        int cdfMin = CalculateCdf(
            ref MemoryMarshal.GetReference(cdfBuffer.GetSpan()),
            ref MemoryMarshal.GetReference(histogram),
            histogram.Length - 1);

        float numberOfPixelsMinusCdfMin = numberOfPixels - cdfMin;

        // Apply the cdf to each pixel of the image
        CdfApplicationRowOperation cdfOperation = new(this.Configuration, interest, cdfBuffer, source.PixelBuffer, this.LuminanceLevels, numberOfPixelsMinusCdfMin);
        ParallelRowIterator.IterateRows<CdfApplicationRowOperation, Vector4>(
            this.Configuration,
            interest,
            in cdfOperation);
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the cdf application levels logic for <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/>.
    /// </summary>
    private readonly struct CdfApplicationRowOperation : IRowOperation<Vector4>
    {
        private readonly Configuration configuration;
        private readonly Rectangle bounds;
        private readonly IMemoryOwner<int> cdfBuffer;
        private readonly Buffer2D<TPixel> source;
        private readonly int luminanceLevels;
        private readonly float numberOfPixelsMinusCdfMin;

        [MethodImpl(InliningOptions.ShortMethod)]
        public CdfApplicationRowOperation(
            Configuration configuration,
            Rectangle bounds,
            IMemoryOwner<int> cdfBuffer,
            Buffer2D<TPixel> source,
            int luminanceLevels,
            float numberOfPixelsMinusCdfMin)
        {
            this.configuration = configuration;
            this.bounds = bounds;
            this.cdfBuffer = cdfBuffer;
            this.source = source;
            this.luminanceLevels = luminanceLevels;
            this.numberOfPixelsMinusCdfMin = numberOfPixelsMinusCdfMin;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds) => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y, Span<Vector4> span)
        {
            Span<Vector4> vectorBuffer = span[..this.bounds.Width];
            ref Vector4 vectorRef = ref MemoryMarshal.GetReference(vectorBuffer);
            ref int cdfBase = ref MemoryMarshal.GetReference(this.cdfBuffer.GetSpan());
            int levels = this.luminanceLevels;
            float noOfPixelsMinusCdfMin = this.numberOfPixelsMinusCdfMin;

            Span<TPixel> pixelRow = this.source.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, pixelRow, vectorBuffer);

            for (int x = 0; x < this.bounds.Width; x++)
            {
                Vector4 vector = Unsafe.Add(ref vectorRef, (uint)x);
                int luminance = ColorNumerics.GetBT709Luminance(ref vector, levels);
                float luminanceEqualized = Unsafe.Add(ref cdfBase, (uint)luminance) / noOfPixelsMinusCdfMin;
                Unsafe.Add(ref vectorRef, (uint)x) = new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, vector.W);
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorBuffer, pixelRow);
        }
    }
}
