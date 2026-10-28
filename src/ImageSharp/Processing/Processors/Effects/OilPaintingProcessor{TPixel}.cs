// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

namespace SixLabors.ImageSharp.Processing.Processors.Effects;

/// <summary>
/// Applies oil painting effect processing to the image.
/// </summary>
/// <remarks>Adapted from <see href="https://softwarebydefault.com/2013/06/29/oil-painting-cartoon-filter/"/> by Dewald Esterhuizen.</remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class OilPaintingProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly OilPaintingProcessor definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="OilPaintingProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="OilPaintingProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public OilPaintingProcessor(Configuration configuration, OilPaintingProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.definition = definition;
    }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        int brushSize = Math.Clamp(this.definition.BrushSize, 1, Math.Min(source.Width, source.Height));

        OilPaintingOperation operation = default;

        // Multiply each channel by one third before summing, so finite HDR components do not overflow their mean.
        Vector3 weights = new(1F / 3F);

        try
        {
            Histogram.Apply<TPixel, OilPaintingBin, OilPaintingBin, OilPaintingOperation>(
                this.Configuration,
                source,
                this.SourceRectangle,
                Math.Clamp(this.definition.Levels, 1, 255),
                weights,
                false,
                0,
                Histogram.Region.Neighborhood(brushSize >> 1),
                in operation);
        }
        catch (Exception ex)
        {
            throw new ImageProcessingException(
                "The OilPaintProcessor failed. The most likely reason is that a pixel component was outside of its' allowed range.",
                ex);
        }
    }

    /// <summary>
    /// Accumulates counts and RGB sums, then averages the selected bin without changing alpha.
    /// </summary>
    private readonly struct OilPaintingOperation : IHistogramOperation<OilPaintingBin, OilPaintingBin>
    {
        /// <inheritdoc />
        public OilPaintingBin GetResult(ref Histogram<OilPaintingBin>.Query histogram, Vector4 source, int index) => histogram.GetMostFrequent();

        /// <inheritdoc />
        public Vector4 Invoke(Vector4 source, in OilPaintingBin data)
            => new(data.Red / data.Count, data.Green / data.Count, data.Blue / data.Count, source.W);

        /// <inheritdoc />
        public Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<OilPaintingBin> data)
        {
            // Each bin stores [integer count, red sum, green sum, blue sum] in one 128-bit lane.
            ref float first = ref Unsafe.As<OilPaintingBin, float>(ref MemoryMarshal.GetReference(data));
            Vector128<float> bins = Vector128.LoadUnsafe(ref first);
            Vector128<float> counts = Vector128.ConvertToSingle(Vector128_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0000).AsInt32());

            // Rotate each bin's RGB sums into RGBA positions, then restore the source alpha after division.
            Vector128<float> sums = Vector128_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0321);
            Vector128<float> alpha = Vector128.Create(0, 0, 0, -1).AsSingle();
            return Vector128.ConditionalSelect(alpha, source, sums / counts);
        }

        /// <inheritdoc />
        public Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<OilPaintingBin> data)
        {
            // Each bin stores [integer count, red sum, green sum, blue sum] in one 128-bit lane.
            ref float first = ref Unsafe.As<OilPaintingBin, float>(ref MemoryMarshal.GetReference(data));
            Vector256<float> bins = Vector256.LoadUnsafe(ref first);
            Vector256<float> counts = Vector256.ConvertToSingle(Vector256_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0000).AsInt32());

            // Rotate each bin's RGB sums into RGBA positions, then restore the source alpha after division.
            Vector256<float> sums = Vector256_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0321);
            Vector256<float> alpha = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector256.ConditionalSelect(alpha, source, sums / counts);
        }

        /// <inheritdoc />
        public Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<OilPaintingBin> data)
        {
            // Each bin stores [integer count, red sum, green sum, blue sum] in one 128-bit lane.
            ref float first = ref Unsafe.As<OilPaintingBin, float>(ref MemoryMarshal.GetReference(data));
            Vector512<float> bins = Vector512.LoadUnsafe(ref first);
            Vector512<float> counts = Vector512.ConvertToSingle(Vector512_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0000).AsInt32());

            // Rotate each bin's RGB sums into RGBA positions, then restore the source alpha after division.
            Vector512<float> sums = Vector512_.ShuffleNative(bins, SimdUtils.Shuffle.MMShuffle0321);
            Vector512<float> alpha = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alpha, source, sums / counts);
        }
    }

    /// <summary>
    /// Stores a neighborhood's count and original channel sums together in each bin.
    /// </summary>
    private struct OilPaintingBin : IHistogramBin<OilPaintingBin>
    {
        public int Count;
        public float Red;
        public float Green;
        public float Blue;

        /// <inheritdoc />
        public static bool RequiresSamples => true;

        /// <inheritdoc />
        int IHistogramBin<OilPaintingBin>.Count
        {
            get => this.Count;
            set => this.Count = value;
        }

        /// <inheritdoc />
        public void Merge(in OilPaintingBin source)
        {
            this.Count += source.Count;
            this.Red += source.Red;
            this.Green += source.Green;
            this.Blue += source.Blue;
        }

        /// <inheritdoc />
        public int Add(Vector4 sample, int delta)
        {
            this.Count += delta;
            this.Red += sample.X * delta;
            this.Blue += sample.Z * delta;
            this.Green += sample.Y * delta;
            return this.Count;
        }
    }
}
