// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

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
    /// <param name="syncChannels">Whether to apply a synchronized luminance value to each color channel.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public AutoLevelProcessor(
        Configuration configuration,
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        bool syncChannels,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
        => this.SyncChannels = syncChannels;

    /// <summary>
    /// Gets a value indicating whether to apply a synchronized luminance value to each color channel.
    /// </summary>
    private bool SyncChannels { get; }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Operation operation = new(source.Width * source.Height, this.LuminanceLevels, this.SyncChannels);
        Vector3 weights = new(ColorNumerics.Bt709.X, ColorNumerics.Bt709.Y, ColorNumerics.Bt709.Z);

        Histogram.Apply<TPixel, Histogram.CountBin, Vector4, Operation>(
            this.Configuration,
            source,
            this.SourceRectangle,
            this.LuminanceLevels,
            weights,
            this.ClipHistogramEnabled,
            this.ClipLimit,
            Histogram.Region.Global,
            in operation);
    }

    /// <summary>
    /// Calculates synchronized or independent channel levels from the shared cumulative histogram.
    /// </summary>
    /// <param name="area">The existing whole-frame normalization area.</param>
    /// <param name="levels">The bounded measurement resolution.</param>
    /// <param name="syncChannels">Whether RGB uses one luminance scaling factor.</param>
    private readonly struct Operation(int area, int levels, bool syncChannels) : IHistogramOperation<Histogram.CountBin, Vector4>
    {
        /// <inheritdoc />
        public static bool UsesCumulativeCounts => true;

        /// <inheritdoc />
        public Vector4 GetResult(ref Histogram<Histogram.CountBin>.Query histogram, Vector4 source, int index)
        {
            if (syncChannels)
            {
                Histogram.Cdf cdf = histogram.GetCdf(index);
                float target = cdf.Cumulative / (float)(area - cdf.Minimum);
                if (histogram.IsExtended)
                {
                    // Extended indices encode magnitude, rather than a linear brightness scale.
                    // CDF output is at most one. Multiply before division to avoid rounding
                    // a tiny reciprocal for large HDR samples. Zero luminance retains the black result.
                    float luminance = ColorNumerics.GetBT709Luminance(source);
                    Vector4 result = luminance == 0F ? Vector4.Zero : (source * target) / luminance;
                    result.W = source.W;
                    return result;
                }

                // Bounded input retains the original quantized luminance calculation.
                // Its first index maps to black and must not be used as a divisor.
                float factor = index == 0 ? 0F : target * levels / index;
                Vector4 boundedResult = source * factor;
                boundedResult.W = source.W;
                return boundedResult;
            }

            Histogram.Cdf red = histogram.GetCdf(histogram.GetIndex(source.X));
            Histogram.Cdf green = histogram.GetCdf(histogram.GetIndex(source.Y));
            Histogram.Cdf blue = histogram.GetCdf(histogram.GetIndex(source.Z));

            // RGB shares the measured luminance distribution but queries each channel separately.
            // Vector division applies their independent denominators; alpha bypasses the calculation.
            Vector4 numerator = new(red.Cumulative, green.Cumulative, blue.Cumulative, 0F);
            Vector4 denominator = new(area - red.Minimum, area - green.Minimum, area - blue.Minimum, 1F);
            Vector4 output = numerator / denominator;
            output.W = source.W;
            return output;
        }

        /// <inheritdoc />
        public Vector4 Invoke(Vector4 source, in Vector4 data) => data;

        /// <inheritdoc />
        public Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<Vector4> data)
        {
            // Each result already contains RGB and unchanged source alpha in RGBA order.
            ref float first = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(data));
            return Vector128.LoadUnsafe(ref first);
        }

        /// <inheritdoc />
        public Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<Vector4> data)
        {
            // Two contiguous RGBA results occupy the complete register.
            ref float first = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(data));
            return Vector256.LoadUnsafe(ref first);
        }

        /// <inheritdoc />
        public Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<Vector4> data)
        {
            // Four contiguous RGBA results occupy the complete register.
            ref float first = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(data));
            return Vector512.LoadUnsafe(ref first);
        }
    }
}
