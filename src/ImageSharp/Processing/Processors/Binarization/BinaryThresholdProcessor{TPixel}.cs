// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        byte threshold = (byte)MathF.Round(this.definition.Threshold * 255F);
        TPixel upper = this.definition.UpperColor.ToPixel<TPixel>();
        TPixel lower = this.definition.LowerColor.ToPixel<TPixel>();

        Rectangle sourceRectangle = this.SourceRectangle;
        Configuration configuration = this.Configuration;

        Rectangle interest = Rectangle.Intersect(sourceRectangle, source.Bounds);
        RowOperation operation = new(
            interest.X,
            source.PixelBuffer,
            upper,
            lower,
            threshold,
            this.definition.Mode,
            configuration);

        ParallelRowIterator.IterateRows<RowOperation, Rgb24>(
            configuration,
            interest,
            in operation);
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the clone logic for <see cref="BinaryThresholdProcessor{TPixel}"/>.
    /// </summary>
    private readonly struct RowOperation : IRowOperation<Rgb24>
    {
        private readonly Buffer2D<TPixel> source;
        private readonly TPixel upper;
        private readonly TPixel lower;
        private readonly byte threshold;
        private readonly BinaryThresholdMode mode;
        private readonly int startX;
        private readonly Configuration configuration;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            int startX,
            Buffer2D<TPixel> source,
            TPixel upper,
            TPixel lower,
            byte threshold,
            BinaryThresholdMode mode,
            Configuration configuration)
        {
            this.startX = startX;
            this.source = source;
            this.upper = upper;
            this.lower = lower;
            this.threshold = threshold;
            this.mode = mode;
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetRequiredBufferLength(Rectangle bounds)
            => bounds.Width;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int y, Span<Rgb24> span)
        {
            TPixel upper = this.upper;
            TPixel lower = this.lower;

            Span<TPixel> rowSpan = this.source.DangerousGetRowSpan(y).Slice(this.startX, span.Length);
            PixelOperations<TPixel>.Instance.ToRgb24(this.configuration, rowSpan, span);

            switch (this.mode)
            {
                case BinaryThresholdMode.Luminance:
                {
                    byte threshold = this.threshold;
                    for (int x = 0; x < rowSpan.Length; x++)
                    {
                        Rgb24 rgb = span[x];
                        byte luminance = ColorNumerics.Get8BitBT709Luminance(rgb.R, rgb.G, rgb.B);
                        ref TPixel color = ref rowSpan[x];
                        color = luminance >= threshold ? upper : lower;
                    }

                    break;
                }

                case BinaryThresholdMode.Saturation:
                {
                    float threshold = this.threshold / 255F;
                    for (int x = 0; x < rowSpan.Length; x++)
                    {
                        float saturation = GetSaturation(span[x]);
                        ref TPixel color = ref rowSpan[x];
                        color = saturation >= threshold ? upper : lower;
                    }

                    break;
                }

                case BinaryThresholdMode.MaxChroma:
                {
                    float threshold = this.threshold * 0.5F;    // /2
                    for (int x = 0; x < rowSpan.Length; x++)
                    {
                        float chroma = GetMaxChroma(span[x]);
                        ref TPixel color = ref rowSpan[x];
                        color = chroma >= threshold ? upper : lower;
                    }

                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetSaturation(Rgb24 rgb)
        {
            // Slimmed down RGB => HSL formula. See HslAndRgbConverter.
            const float inv255 = 1 / 255F;
            float r = rgb.R * inv255;
            float g = rgb.G * inv255;
            float b = rgb.B * inv255;

            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float chroma = max - min;

            if (MathF.Abs(chroma) < Constants.Epsilon)
            {
                return 0F;
            }

            float l = (max + min) * 0.5F;   // /2

            if (l <= .5F)
            {
                return chroma / (max + min);
            }

            return chroma / (2F - max - min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetMaxChroma(Rgb24 rgb)
        {
            // Slimmed down RGB => YCbCr formula. See YCbCrAndRgbConverter.
            float r = rgb.R;
            float g = rgb.G;
            float b = rgb.B;
            const float achromatic = 127.5F;

            float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
            float cr = 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b));

            return MathF.Max(MathF.Abs(cb - achromatic), MathF.Abs(cr - achromatic));
        }
    }
}
