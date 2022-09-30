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
/// Applies a luminance histogram equalization to the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class AutoLevelProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoLevelProcessor{TPixel}"/> class.
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
    /// <param name="syncChannels">Whether to apply a synchronized luminance value to each color channel.</param>
    public AutoLevelProcessor(
        Configuration configuration,
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        bool syncChannels,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
    {
        this.SyncChannels = syncChannels;
    }

    /// <summary>
    /// Gets a value indicating whether to apply a synchronized luminance value to each color channel.
    /// </summary>
    private bool SyncChannels { get; }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        MemoryAllocator memoryAllocator = this.Configuration.MemoryAllocator;
        int numberOfPixels = source.Width * source.Height;
        var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

        using IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean);

        // Build the histogram of the grayscale levels.
        var grayscaleOperation = new GrayscaleLevelsRowOperation<TPixel>(this.Configuration, interest, histogramBuffer, source.PixelBuffer, this.LuminanceLevels);
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

        if (this.SyncChannels)
        {
            var cdfOperation = new SynchronizedChannelsRowOperation(this.Configuration, interest, cdfBuffer, source.PixelBuffer, this.LuminanceLevels, numberOfPixelsMinusCdfMin);
            ParallelRowIterator.IterateRows<SynchronizedChannelsRowOperation, Vector4>(
                this.Configuration,
                interest,
                in cdfOperation);
        }
        else
        {
            var cdfOperation = new SeperateChannelsRowOperation(this.Configuration, interest, cdfBuffer, source.PixelBuffer, this.LuminanceLevels, numberOfPixelsMinusCdfMin);
            ParallelRowIterator.IterateRows<SeperateChannelsRowOperation, Vector4>(
                this.Configuration,
                interest,
                in cdfOperation);
        }
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the cdf logic for synchronized color channels.
    /// </summary>
    private readonly struct SynchronizedChannelsRowOperation : IRowOperation<Vector4>
    {
        private readonly Configuration configuration;
        private readonly Rectangle bounds;
        private readonly IMemoryOwner<int> cdfBuffer;
        private readonly Buffer2D<TPixel> source;
        private readonly int luminanceLevels;
        private readonly float numberOfPixelsMinusCdfMin;

        [MethodImpl(InliningOptions.ShortMethod)]
        public SynchronizedChannelsRowOperation(
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
            Span<Vector4> vectorBuffer = span.Slice(0, this.bounds.Width);
            ref Vector4 vectorRef = ref MemoryMarshal.GetReference(vectorBuffer);
            ref int cdfBase = ref MemoryMarshal.GetReference(this.cdfBuffer.GetSpan());
            var sourceAccess = new PixelAccessor<TPixel>(this.source);
            int levels = this.luminanceLevels;
            float noOfPixelsMinusCdfMin = this.numberOfPixelsMinusCdfMin;

            Span<TPixel> pixelRow = sourceAccess.GetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, pixelRow, vectorBuffer);

            for (int x = 0; x < this.bounds.Width; x++)
            {
                var vector = Unsafe.Add(ref vectorRef, x);
                int luminance = ColorNumerics.GetBT709Luminance(ref vector, levels);
                float scaledLuminance = Unsafe.Add(ref cdfBase, luminance) / noOfPixelsMinusCdfMin;
                float scalingFactor = scaledLuminance * levels / luminance;
                Vector4 scaledVector = new Vector4(scalingFactor * vector.X, scalingFactor * vector.Y, scalingFactor * vector.Z, vector.W);
                Unsafe.Add(ref vectorRef, x) = scaledVector;
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorBuffer, pixelRow);
        }
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the cdf logic for separate color channels.
    /// </summary>
    private readonly struct SeperateChannelsRowOperation : IRowOperation<Vector4>
    {
        private readonly Configuration configuration;
        private readonly Rectangle bounds;
        private readonly IMemoryOwner<int> cdfBuffer;
        private readonly Buffer2D<TPixel> source;
        private readonly int luminanceLevels;
        private readonly float numberOfPixelsMinusCdfMin;

        [MethodImpl(InliningOptions.ShortMethod)]
        public SeperateChannelsRowOperation(
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
            Span<Vector4> vectorBuffer = span.Slice(0, this.bounds.Width);
            ref Vector4 vectorRef = ref MemoryMarshal.GetReference(vectorBuffer);
            ref int cdfBase = ref MemoryMarshal.GetReference(this.cdfBuffer.GetSpan());
            var sourceAccess = new PixelAccessor<TPixel>(this.source);
            int levelsMinusOne = this.luminanceLevels - 1;
            float noOfPixelsMinusCdfMin = this.numberOfPixelsMinusCdfMin;

            Span<TPixel> pixelRow = sourceAccess.GetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, pixelRow, vectorBuffer);

            for (int x = 0; x < this.bounds.Width; x++)
            {
                var vector = Unsafe.Add(ref vectorRef, x) * levelsMinusOne;

                uint originalX = (uint)MathF.Round(vector.X);
                float scaledX = Unsafe.Add(ref cdfBase, originalX) / noOfPixelsMinusCdfMin;
                uint originalY = (uint)MathF.Round(vector.Y);
                float scaledY = Unsafe.Add(ref cdfBase, originalY) / noOfPixelsMinusCdfMin;
                uint originalZ = (uint)MathF.Round(vector.Z);
                float scaledZ = Unsafe.Add(ref cdfBase, originalZ) / noOfPixelsMinusCdfMin;
                Unsafe.Add(ref vectorRef, x) = new Vector4(scaledX, scaledY, scaledZ, vector.W);
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorBuffer, pixelRow);
        }
    }
}
