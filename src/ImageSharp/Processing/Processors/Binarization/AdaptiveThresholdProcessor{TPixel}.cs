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
/// <typeparam name="TPixel">The pixel format.</typeparam>
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
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);

        Configuration configuration = this.Configuration;
        TPixel upper = this.definition.Upper.ToPixel<TPixel>();
        TPixel lower = this.definition.Lower.ToPixel<TPixel>();
        float thresholdLimit = this.definition.ThresholdLimit;

        // Use a centered window with at least one neighboring pixel on each side, where the bounds allow it.
        // Retain the radius cap of 255 so the window does not keep growing for images wider than 4096 pixels.
        byte windowRadius = (byte)Math.Clamp(interest.Width / 16, 1, 255);

        using Buffer2D<ulong> intImage = source.CalculateIntegralImage(interest);
        RowOperation operation = new(configuration, interest, source.PixelBuffer, intImage, upper, lower, thresholdLimit, windowRadius);
        ParallelRowIterator.IterateRows<RowOperation, L8>(configuration, interest, in operation);
    }

    /// <summary>
    /// Applies the local threshold using the inclusive sums stored in the integral image.
    /// </summary>
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
        private readonly byte windowRadius;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowOperation"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for bulk luminance conversion.</param>
        /// <param name="bounds">The image rectangle to process.</param>
        /// <param name="source">The pixel buffer to update.</param>
        /// <param name="intImage">The inclusive integral image, with coordinates relative to <paramref name="bounds"/>.</param>
        /// <param name="upper">The output for samples above the local threshold.</param>
        /// <param name="lower">The output for samples at or below the local threshold.</param>
        /// <param name="thresholdLimit">The fraction of the local mean used as the threshold.</param>
        /// <param name="windowRadius">The number of neighboring pixels to include on each side.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            Configuration configuration,
            Rectangle bounds,
            Buffer2D<TPixel> source,
            Buffer2D<ulong> intImage,
            TPixel upper,
            TPixel lower,
            float thresholdLimit,
            byte windowRadius)
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
            this.windowRadius = windowRadius;
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
            int windowRadius = this.windowRadius;
            float thresholdLimit = this.thresholdLimit;

            // Integral coordinates are relative to the processing rectangle. Include the current pixel
            // and clip each side of its centered window to that rectangle, including one-pixel dimensions.
            int relativeY = y - this.startY;
            int top = Math.Max(relativeY - windowRadius, 0);
            int bottom = Math.Min(relativeY + windowRadius, this.bounds.Height - 1);
            uint windowHeight = (uint)(bottom - top + 1);

            // I(x,y) includes row y and column x. Subtract the prefix ending just before the window.
            // An empty prefix span represents the image's top boundary. Acquire both spans once per row.
            ReadOnlySpan<ulong> bottomRow = this.intImage.DangerousGetRowSpan(bottom);
            ReadOnlySpan<ulong> precedingRow = top > 0 ? this.intImage.DangerousGetRowSpan(top - 1) : default;

            for (int x = 0; x < rowSpan.Length; x++)
            {
                int left = Math.Max(x - windowRadius, 0);
                int right = Math.Min(x + windowRadius, maxX);
                uint count = (uint)(right - left + 1) * windowHeight;

                ulong sum = bottomRow[right] - (precedingRow.IsEmpty ? 0 : precedingRow[right]);
                if (left > 0)
                {
                    // Remove the columns before the window from the same vertical band. Subtracting
                    // these nonnegative band sums avoids unsigned underflow between integral corners.
                    sum -= bottomRow[left - 1] - (precedingRow.IsEmpty ? 0 : precedingRow[left - 1]);
                }

                // The inclusive window always contains the current pixel, so count cannot be zero.
                if (span[x].PackedValue * count <= sum * thresholdLimit)
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
