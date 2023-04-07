// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization;

/// <summary>
/// Performs Bradley Adaptive Threshold filter against an image.
/// </summary>
internal class AdaptiveThresholdProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly AdaptiveThresholdProcessor definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="AdaptiveThresholdProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public AdaptiveThresholdProcessor(Configuration configuration, AdaptiveThresholdProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
        => this.definition = definition;

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

        Configuration configuration = this.Configuration;
        TPixel upper = this.definition.Upper.ToPixel<TPixel>();
        TPixel lower = this.definition.Lower.ToPixel<TPixel>();
        float thresholdLimit = this.definition.ThresholdLimit;

        // ClusterSize defines the size of cluster to used to check for average.
        // Tweaked to support up to 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
        byte clusterSize = (byte)Math.Clamp(interest.Width / 16F, 0, 255);

        using Buffer2D<ulong> intImage = source.CalculateIntegralImage(interest);
        RowOperation operation = new(configuration, interest, source.PixelBuffer, intImage, upper, lower, thresholdLimit, clusterSize);
        ParallelRowIterator.IterateRows<RowOperation, L8>(
            configuration,
            interest,
            in operation);
    }

    private readonly struct RowOperation : IRowOperation<L8>
    {
        private readonly Configuration configuration;
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> source;
        private readonly Buffer2D<ulong> intImage;
        private readonly TPixel upper;
        private readonly TPixel lower;
        private readonly float thresholdLimit;
        private readonly int startX;
        private readonly int startY;
        private readonly byte clusterSize;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            Configuration configuration,
            Rectangle bounds,
            Buffer2D<TPixel> source,
            Buffer2D<ulong> intImage,
            TPixel upper,
            TPixel lower,
            float thresholdLimit,
            byte clusterSize)
        {
            this.configuration = configuration;
            this.bounds = bounds;
            this.startX = bounds.X;
            this.startY = bounds.Y;
            this.source = source;
            this.intImage = intImage;
            this.upper = upper;
            this.lower = lower;
            this.thresholdLimit = thresholdLimit;
            this.clusterSize = clusterSize;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds)
            => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y, Span<L8> span)
        {
            Span<TPixel> rowSpan = this.source.DangerousGetRowSpan(y).Slice(this.startX, span.Length);
            PixelOperations<TPixel>.Instance.ToL8(this.configuration, rowSpan, span);

            int maxX = this.bounds.Width - 1;
            int maxY = this.bounds.Height - 1;
            for (int x = 0; x < rowSpan.Length; x++)
            {
                int x1 = Math.Clamp(x - this.clusterSize + 1, 0, maxX);
                int x2 = Math.Min(x + this.clusterSize + 1, maxX);
                int y1 = Math.Clamp(y - this.startY - this.clusterSize + 1, 0, maxY);
                int y2 = Math.Min(y - this.startY + this.clusterSize + 1, maxY);

                uint count = (uint)((x2 - x1) * (y2 - y1));
                ulong sum = Math.Min(this.intImage[x2, y2] - this.intImage[x1, y2] - this.intImage[x2, y1] + this.intImage[x1, y1], ulong.MaxValue);

                if (span[x].PackedValue * count <= sum * this.thresholdLimit)
                {
                    rowSpan[x] = this.lower;
                }
                else
                {
                    rowSpan[x] = this.upper;
                }
            }
        }
    }
}
