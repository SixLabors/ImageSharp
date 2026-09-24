// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization;

/// <summary>
/// Performs simple binary threshold filtering against an image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class BinaryThresholdProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly BinaryThresholdProcessor definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="BinaryThresholdProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public BinaryThresholdProcessor(Configuration configuration, BinaryThresholdProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
        => this.definition = definition;

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);
        if (interest.IsEmpty)
        {
            return;
        }

        Configuration configuration = this.Configuration;
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;
        PixelConversionModifiers modifiers = PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply;
        using IMemoryOwner<Vector4> rowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(interest.Width);
        Span<Vector4> vectors = rowBuffer.GetSpan()[..interest.Width];

        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        for (int y = interest.Top; y < interest.Bottom; y++)
        {
            Span<TPixel> row = source.PixelBuffer.DangerousGetRowSpan(y).Slice(interest.X, interest.Width);
            operations.ToVector4(configuration, row, vectors, modifiers);

            for (int x = 0; x < vectors.Length; x++)
            {
                float metric = GetMetric(vectors[x], this.definition.Mode);
                if (float.IsFinite(metric))
                {
                    minimum = MathF.Min(minimum, metric);
                    maximum = MathF.Max(maximum, metric);
                }
            }
        }

        // Compute in double precision so a finite range spanning large negative and
        // positive HDR values does not overflow before its percentage is applied.
        bool hasFiniteValue = minimum <= maximum;
        float threshold = hasFiniteValue
            ? (float)(minimum + (this.definition.Threshold * ((double)maximum - minimum)))
            : 0F;

        RowOperation operation = new(
            interest.X,
            source.PixelBuffer,
            this.definition.UpperColor.ToPixel<TPixel>(),
            this.definition.LowerColor.ToPixel<TPixel>(),
            threshold,
            hasFiniteValue,
            this.definition.Mode,
            configuration);

        ParallelRowIterator.IterateRows<RowOperation, Vector4>(configuration, interest, in operation);
    }

    /// <summary>
    /// Gets the selected metric without using the alpha component.
    /// </summary>
    /// <param name="vector">The source pixel components.</param>
    /// <param name="mode">The selected threshold metric.</param>
    /// <returns>The metric value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetMetric(Vector4 vector, BinaryThresholdMode mode)
    {
        // The alpha component is not part of any threshold metric. Clear it before
        // the luminance dot product so even a nonfinite alpha cannot affect the result.
        vector.W = 0F;
        return mode switch
        {
            BinaryThresholdMode.Saturation => GetSaturation(vector),
            BinaryThresholdMode.MaxChroma => GetMaxChroma(vector),
            _ => ColorNumerics.GetBT709Luminance(vector)
        };
    }

    /// <summary>
    /// Gets HSL saturation from the original color components.
    /// </summary>
    /// <param name="vector">The source pixel components.</param>
    /// <returns>The saturation value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetSaturation(Vector4 vector)
    {
        // Retain the existing HSL saturation metric while reading original float values.
        float max = MathF.Max(vector.X, MathF.Max(vector.Y, vector.Z));
        float min = MathF.Min(vector.X, MathF.Min(vector.Y, vector.Z));
        float chroma = max - min;

        if (MathF.Abs(chroma) < Constants.Epsilon)
        {
            return 0F;
        }

        float lightness = (max + min) * 0.5F;
        return lightness <= .5F
            ? chroma / (max + min)
            : chroma / (2F - max - min);
    }

    /// <summary>
    /// Gets the larger magnitude of the YCbCr chroma components.
    /// </summary>
    /// <param name="vector">The source pixel components.</param>
    /// <returns>The chroma magnitude.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetMaxChroma(Vector4 vector)
    {
        // Remove the neutral chroma offsets from the existing YCbCr formula.
        float cb = (-0.168736F * vector.X) - (0.331264F * vector.Y) + (0.5F * vector.Z);
        float cr = (0.5F * vector.X) - (0.418688F * vector.Y) - (0.081312F * vector.Z);
        return MathF.Max(MathF.Abs(cb), MathF.Abs(cr));
    }

    /// <summary>
    /// Applies the measured threshold to one pixel row.
    /// </summary>
    private readonly struct RowOperation : IRowOperation<Vector4>
    {
        private readonly Buffer2D<TPixel> source;
        private readonly TPixel upper;
        private readonly TPixel lower;
        private readonly float threshold;
        private readonly bool hasFiniteValue;
        private readonly BinaryThresholdMode mode;
        private readonly int startX;
        private readonly Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowOperation"/> struct with the measured threshold.
        /// </summary>
        /// <param name="startX">The first pixel of the selected region.</param>
        /// <param name="source">The source pixel buffer.</param>
        /// <param name="upper">The color for values at or above the threshold.</param>
        /// <param name="lower">The color for values below the threshold.</param>
        /// <param name="threshold">The threshold in the selected metric's range.</param>
        /// <param name="hasFiniteValue">Whether the selected region contained a finite metric.</param>
        /// <param name="mode">The selected threshold metric.</param>
        /// <param name="configuration">The shared configuration.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            int startX,
            Buffer2D<TPixel> source,
            TPixel upper,
            TPixel lower,
            float threshold,
            bool hasFiniteValue,
            BinaryThresholdMode mode,
            Configuration configuration)
        {
            this.startX = startX;
            this.source = source;
            this.upper = upper;
            this.lower = lower;
            this.threshold = threshold;
            this.hasFiniteValue = hasFiniteValue;
            this.mode = mode;
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds) => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int y, Span<Vector4> span)
        {
            Span<TPixel> row = this.source.DangerousGetRowSpan(y).Slice(this.startX, span.Length);
            PixelOperations<TPixel>.Instance.ToVector4(
                this.configuration,
                row,
                span,
                PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

            for (int x = 0; x < row.Length; x++)
            {
                float metric = GetMetric(span[x], this.mode);
                row[x] = this.hasFiniteValue && float.IsFinite(metric) && metric >= this.threshold
                    ? this.upper
                    : this.lower;
            }
        }
    }
}
