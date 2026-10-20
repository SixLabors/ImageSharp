// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Histograms;

/// <summary>
/// Owns histogram traversal, scheduling and output packing for every constrained consumer.
/// </summary>
/// <remarks>
/// <para>
/// Pixel conversion supplies straight RGB components. The supplied weights reduce RGB to one measurement;
/// alpha does not contribute. The same mapping produces indices for accumulation and output queries.
/// <see cref="PixelTypeInfo.ColorRange"/> selects the mapping, independently of the pixel's bit depth.
/// </para>
/// <para>
/// SDR measurements use the requested number of bins across [0,1]. With 256 bins, measurements 0, 0.5 and 1
/// select bins 0, 128 and 255. Extended measurements use signed bins whose spacing grows with magnitude.
/// Each doubling interval has 128 bins: [1,2) has spacing 1/128, while [2,4) has spacing 2/128.
/// Negative measurements use the same spacing in reverse order. Both signed zeros share the center bin.
/// This mapping covers finite float values without scanning the image for a range or clipping source RGB.
/// Binning groups nearby measurements; it does not replace the source colors with bin values.
/// </para>
/// <para>
/// The region selects which samples contribute to each result. Global processing accumulates one histogram
/// before writing output. A sliding window subtracts samples that leave and adds samples that enter.
/// Neighborhood processing rebuilds counts in source order for each pixel, reusing the same storage.
/// Tiled processing interpolates results from adjacent tile histograms.
/// </para>
/// <para>
/// This type owns traversal and schedules independent intervals through <see cref="ParallelRowIterator"/>.
/// Each active interval has its own writable histogram. The consumer supplies only bin arithmetic and output
/// calculations through the constrained operation types. Scalar and SIMD paths use the same mapping rules.
/// <see cref="Histogram{TBin}"/> owns allocator-backed storage and supplies borrowed spans to the loops.
/// </para>
/// </remarks>
internal static partial class Histogram
{
    /// <summary>
    /// Identifies the source sampling rule. Consumer callbacks use the same contract for every rule.
    /// </summary>
    public enum RegionKind
    {
        /// <summary>
        /// One histogram covers the processing rectangle.
        /// </summary>
        Global,

        /// <summary>
        /// One histogram moves between adjacent output pixels.
        /// </summary>
        SlidingWindow,

        /// <summary>
        /// A neighborhood is accumulated in source order for each output pixel.
        /// </summary>
        Neighborhood,

        /// <summary>
        /// Adjacent tile histograms supply interpolated output calculations.
        /// </summary>
        Tiles
    }

    /// <summary>
    /// Applies custom bin arithmetic and output calculations while owning storage, sampling and scheduling.
    /// </summary>
    /// <typeparam name="TPixel">The source and destination pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin accumulator.</typeparam>
    /// <typeparam name="TData">The calculation data passed to scalar and SIMD output methods.</typeparam>
    /// <typeparam name="TOperation">The constrained consumer calculation.</typeparam>
    /// <param name="configuration">The configured allocator, pixel operations and parallel execution settings.</param>
    /// <param name="source">The frame to process.</param>
    /// <param name="bounds">The requested output region.</param>
    /// <param name="levels">The bounded measurement resolution.</param>
    /// <param name="weights">The RGB measurement coefficients.</param>
    /// <param name="clipHistogram">Whether to cap and redistribute histogram counts.</param>
    /// <param name="clipLimit">The configured count limit when clipping is enabled.</param>
    /// <param name="region">The source sampling rule.</param>
    /// <param name="operation">The consumer's calculations, which allocate no storage and traverse no image rows.</param>
    public static void Apply<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        ImageFrame<TPixel> source,
        Rectangle bounds,
        int levels,
        Vector3 weights,
        bool clipHistogram,
        int clipLimit,
        Region region,
        in TOperation operation)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        HistogramIndexMapping mapping = GetMapping<TPixel>(levels);
        Rectangle interest = Rectangle.Intersect(bounds, source.Bounds);
        if (region.Kind == RegionKind.SlidingWindow)
        {
            RunSlidingWindow<TPixel, TBin, TData, TOperation>(
                configuration,
                source.PixelBuffer,
                mapping,
                weights,
                clipHistogram,
                clipLimit,
                region.Range,
                in operation);

            return;
        }

        if (region.Kind == RegionKind.Tiles)
        {
            RunTiles<TPixel, TBin, TData, TOperation>(
                configuration,
                source.PixelBuffer,
                mapping,
                weights,
                clipHistogram,
                clipLimit,
                region.Range,
                in operation);

            return;
        }

        if (region.Kind == RegionKind.Global)
        {
            // Accumulate all intervals before producing output. The completed counts are then read-only,
            // so output intervals can query the same histogram without locks.
            using Histogram<TBin> histogram = new(configuration, mapping, 0);
            ConsumerAccumulation<TPixel, TBin> accumulation = new(configuration, source.PixelBuffer, interest, mapping, weights, histogram);
            ParallelRowIterator.IterateRowIntervals(configuration, interest, in accumulation);

            Histogram<TBin>.Data data = histogram.GetData();
            if (clipHistogram)
            {
                data.Clip(clipLimit);
            }

            // A fixed histogram can replace its bin counts with cumulative counts once. Moving windows
            // retain their original counts because later steps must subtract departing samples.
            if (TOperation.UsesCumulativeCounts)
            {
                data.PrepareCdf();
            }

            ConsumerRows<TPixel, TBin, TData, TOperation> rows = new(
                configuration, source.PixelBuffer, source.PixelBuffer, interest, mapping, weights, histogram, 0, false, clipLimit, operation);

            ParallelRowIterator.IterateRowIntervals<ConsumerRows<TPixel, TBin, TData, TOperation>, Vector4>(configuration, interest, in rows);
        }
        else
        {
            using Buffer2D<TPixel> target = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Size);

            // Neighborhoods must read unchanged pixels. Retain source pixels outside the requested region.
            source.CopyTo(target);
            ConsumerRows<TPixel, TBin, TData, TOperation> rows = new(
                configuration, source.PixelBuffer, target, interest, mapping, weights, null, region.Range, clipHistogram, clipLimit, operation);

            ParallelRowIterator.IterateRowIntervals<ConsumerRows<TPixel, TBin, TData, TOperation>, Vector4>(configuration, interest, in rows);
            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, target);
        }
    }

    /// <summary>
    /// Gets reusable vector and index storage for one conversion batch.
    /// </summary>
    /// <param name="length">The maximum sample count.</param>
    /// <returns>The combined length in Vector4 elements.</returns>
    public static int GetSampleBufferLength(int length) => length + ((length + 3) / 4);

    /// <summary>
    /// Gets the byte count for one reusable conversion batch.
    /// </summary>
    /// <param name="length">The maximum sample count.</param>
    /// <returns>The combined buffer size in bytes.</returns>
    public static int GetSampleByteLength(int length) => GetSampleBufferLength(length) * Unsafe.SizeOf<Vector4>();

    /// <summary>
    /// Gets the integer region after the source vectors.
    /// </summary>
    /// <param name="buffer">The combined sample buffer.</param>
    /// <param name="length">The source vector count.</param>
    /// <returns>The matching index storage.</returns>
    public static Span<int> GetIndices(Span<Vector4> buffer, int length) => MemoryMarshal.Cast<Vector4, int>(buffer[length..])[..length];

    /// <summary>
    /// Selects bounded bins for SDR pixels and signed magnitude bins for extended pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format that supplies the color-range contract.</typeparam>
    /// <param name="levels">The requested bin count for SDR pixels.</param>
    /// <returns>The mapping used for both accumulation and queries.</returns>
    private static HistogramIndexMapping GetMapping<TPixel>(int levels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Seven fraction bits retain 128 bins per doubling interval without scanning the image range.
        // ColorRange supplies the range contract. The pixel's bit depth or vector conversion name
        // does not establish whether its RGB values stay within [0,1].
        const int ExtendedFractionBits = 7;
        return new HistogramIndexMapping(levels, TPixel.GetPixelTypeInfo().ColorRange == PixelColorRange.Extended ? ExtendedFractionBits : -1);
    }

    /// <summary>
    /// Applies an output calculation to a block. Source and destination can be the same span.
    /// </summary>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <typeparam name="TData">The histogram result for each pixel.</typeparam>
    /// <typeparam name="TOperation">The scalar and SIMD output calculation.</typeparam>
    /// <param name="source">The straight source colors.</param>
    /// <param name="data">One histogram result for each source color.</param>
    /// <param name="destination">The output colors.</param>
    /// <param name="operation">The constrained output calculation.</param>
    private static void ApplyConsumerPixels<TBin, TData, TOperation>(
        ReadOnlySpan<Vector4> source,
        ReadOnlySpan<TData> data,
        Span<Vector4> destination,
        in TOperation operation)
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        ReadOnlySpan<float> components = MemoryMarshal.Cast<Vector4, float>(source);
        ref float first = ref MemoryMarshal.GetReference(components);
        ref float target = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector512Count<float>();

            // Each 128-bit lane holds one RGBA color. Load the complete group before writing an in-place result.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> input = Vector512.LoadUnsafe(ref first, (uint)i);
                ReadOnlySpan<TData> results = data.Slice(i / 4, Vector512<float>.Count / 4);
                Unsafe.AsRef(in operation).Invoke(input, results).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector256Count<float>();

            // Each 128-bit lane holds one RGBA color. Load the complete group before writing an in-place result.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> input = Vector256.LoadUnsafe(ref first, (uint)i);
                ReadOnlySpan<TData> results = data.Slice(i / 4, Vector256<float>.Count / 4);
                Unsafe.AsRef(in operation).Invoke(input, results).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector128Count<float>();

            // Each 128-bit lane holds one RGBA color. Load the complete group before writing an in-place result.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> input = Vector128.LoadUnsafe(ref first, (uint)i);
                ReadOnlySpan<TData> results = data.Slice(i / 4, Vector128<float>.Count / 4);
                Unsafe.AsRef(in operation).Invoke(input, results).StoreUnsafe(ref target, (uint)i);
            }
        }

        // SIMD groups end on pixel boundaries. The fallback uses the same typed result and output calculation.
        for (int pixel = i / 4; pixel < source.Length; pixel++)
        {
            destination[pixel] = Unsafe.AsRef(in operation).Invoke(source[pixel], in data[pixel]);
        }
    }

    /// <summary>
    /// Defines the source region used for each output calculation.
    /// </summary>
    public readonly struct Region
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Region"/> struct.
        /// </summary>
        /// <param name="kind">The sampling rule owned by the histogram runner.</param>
        /// <param name="range">The sampling range described by <see cref="Range"/>.</param>
        private Region(RegionKind kind, int range)
        {
            this.Kind = kind;
            this.Range = range;
        }

        /// <summary>
        /// Gets a region covering the complete processing rectangle.
        /// </summary>
        public static Region Global => new(RegionKind.Global, 0);

        /// <summary>
        /// Gets the sampling rule.
        /// </summary>
        public RegionKind Kind { get; }

        /// <summary>
        /// Gets the sampling range for the selected <see cref="Kind"/>.
        /// </summary>
        /// <remarks>
        /// For <see cref="RegionKind.SlidingWindow"/>, this is the window width in pixels.
        /// For <see cref="RegionKind.Neighborhood"/>, this is the radius in pixels.
        /// For <see cref="RegionKind.Tiles"/>, this is the tile count on each axis.
        /// For <see cref="RegionKind.Global"/>, this is zero and is not used.
        /// </remarks>
        public int Range { get; }

        /// <summary>
        /// Defines a centered moving window with the existing reflected-edge sampling.
        /// </summary>
        /// <param name="width">The sample-window width.</param>
        /// <returns>The window region.</returns>
        public static Region SlidingWindow(int width) => new(RegionKind.SlidingWindow, width);

        /// <summary>
        /// Defines a neighborhood that ends at each output pixel, with clamped source edges.
        /// </summary>
        /// <param name="radius">The number of source positions above and left of the output pixel.</param>
        /// <returns>The neighborhood region.</returns>
        public static Region Neighborhood(int radius) => new(RegionKind.Neighborhood, radius);

        /// <summary>
        /// Defines a tile grid whose output is interpolated between adjacent tile centers.
        /// </summary>
        /// <param name="count">The configured tile count on each axis.</param>
        /// <returns>The tile region.</returns>
        public static Region Tiles(int count) => new(RegionKind.Tiles, count);
    }

    /// <summary>
    /// Stores the cumulative count and its first nonzero count as two consecutive 32-bit integers.
    /// </summary>
    public readonly struct Cdf
    {
        /// <summary>
        /// The cumulative count with the first nonzero count subtracted.
        /// </summary>
        public readonly int Cumulative;

        /// <summary>
        /// The first nonzero count.
        /// </summary>
        public readonly int Minimum;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cdf"/> struct.
        /// </summary>
        /// <param name="cumulative">The cumulative count with the minimum subtracted.</param>
        /// <param name="minimum">The first nonzero count.</param>
        public Cdf(int cumulative, int minimum)
        {
            this.Cumulative = cumulative;
            this.Minimum = minimum;
        }
    }

    /// <summary>
    /// Counts samples for histogram equalization and auto-level calculations.
    /// </summary>
    public struct CountBin : IHistogramBin<CountBin>
    {
        /// <inheritdoc />
        public static bool RequiresSamples => false;

        /// <inheritdoc />
        public static bool TrackMaximum => false;

        /// <inheritdoc />
        public int Count { get; set; }

        /// <inheritdoc />
        public static int SumCounts(ReadOnlySpan<CountBin> bins)
        {
            // Each bin contains exactly one integer count. Use the shared SIMD reduction without a conversion buffer.
            return System.Numerics.Tensors.TensorPrimitives.Sum<int>(MemoryMarshal.Cast<CountBin, int>(bins));
        }

        /// <inheritdoc />
        public static int GetFirstCount(ReadOnlySpan<CountBin> bins)
        {
            // The page total proves that a nonzero count exists. Search the packed counts with the shared SIMD span API.
            ReadOnlySpan<int> counts = MemoryMarshal.Cast<CountBin, int>(bins);
            return counts[counts.IndexOfAnyExcept(0)];
        }

        /// <inheritdoc />
        public int Add(Vector4 sample, int delta) => this.Count += delta;

        /// <inheritdoc />
        public void Merge(in CountBin source) => this.Count += source.Count;
    }

    /// <summary>
    /// Accumulates private interval bins and merges once, outside the sample loops.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <param name="configuration">The allocator and pixel conversion configuration.</param>
    /// <param name="source">The unchanged source pixels.</param>
    /// <param name="bounds">The complete histogram region.</param>
    /// <param name="mapping">The measurement mapping.</param>
    /// <param name="weights">The measurement coefficients.</param>
    /// <param name="target">The shared global histogram.</param>
    private readonly struct ConsumerAccumulation<TPixel, TBin>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        Rectangle bounds,
        HistogramIndexMapping mapping,
        Vector3 weights,
        Histogram<TBin> target) : IRowIntervalOperation
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
    {
        /// <inheritdoc />
        public void Invoke(in RowInterval rows)
        {
            // Each interval updates private counts. Merge once after its rows are complete rather
            // than locking the shared histogram for each sample.
            using Histogram<TBin> local = new(configuration, mapping, bounds.Width);
            Histogram<TBin>.Data data = local.GetData();
            Span<Vector4> colors = data.Vectors[..bounds.Width];
            for (int y = rows.Min; y < rows.Max; y++)
            {
                ReadOnlySpan<TPixel> row = source.DangerousGetRowSpan(y).Slice(bounds.X, bounds.Width);
                ReadOnlySpan<int> indices = data.Map(row, weights);
                data.Add(indices, colors, 1);
            }

            lock (target.SyncRoot)
            {
                // Borrow target spans under the merge lock: a preceding interval may have added blocks.
                Histogram<TBin>.Data destination = target.GetData();
                destination.Merge(ref data);
            }
        }
    }

    /// <summary>
    /// Applies the same consumer contract to prepared histograms and rebuilt neighborhoods.
    /// </summary>
    /// <typeparam name="TPixel">The source and destination pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <typeparam name="TData">The per-pixel calculation data.</typeparam>
    /// <typeparam name="TOperation">The custom calculation.</typeparam>
    /// <param name="configuration">The allocator and pixel conversion configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="target">The output pixels.</param>
    /// <param name="bounds">The output region.</param>
    /// <param name="mapping">The measurement mapping.</param>
    /// <param name="weights">The measurement coefficients.</param>
    /// <param name="global">The prepared histogram, or null for local neighborhoods.</param>
    /// <param name="radius">The neighborhood radius.</param>
    /// <param name="clipHistogram">Whether local queries cap counts.</param>
    /// <param name="clipLimit">The configured count limit.</param>
    /// <param name="operation">The constrained consumer calculation.</param>
    private readonly struct ConsumerRows<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        Buffer2D<TPixel> target,
        Rectangle bounds,
        HistogramIndexMapping mapping,
        Vector3 weights,
        Histogram<TBin>? global,
        int radius,
        bool clipHistogram,
        int clipLimit,
        TOperation operation) : IRowIntervalOperation<Vector4>
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        /// <inheritdoc />
        public int GetRequiredBufferLength(Rectangle rectangle) => Histogram.GetSampleBufferLength(rectangle.Width + radius + 1);

        /// <inheritdoc />
        public void Invoke(in RowInterval rows, Span<Vector4> buffer)
        {
            if (global is not null)
            {
                Histogram<TBin>.Query query = new(global.GetData());
                this.ProcessRows(in rows, buffer, ref query);
            }
            else
            {
                using Histogram<TBin> local = new(configuration, mapping, 0);
                using Histogram<TBin>? clipped = clipHistogram ? new(configuration, mapping, 0) : null;
                Histogram<TBin>.Query query = new(local.GetData());
                if (clipped is not null)
                {
                    query.SetClippedStorage(clipped.GetData());
                }

                this.ProcessRows(in rows, buffer, ref query);
            }
        }

        /// <summary>
        /// Converts rows, obtains custom calculation data and packs output through the shared bulk pixel APIs.
        /// </summary>
        /// <param name="rows">The assigned output rows.</param>
        /// <param name="buffer">The row and measurement spans rented by the histogram's row iterator.</param>
        /// <param name="query">The borrowed histogram spans, retained for the complete interval.</param>
        private void ProcessRows(in RowInterval rows, Span<Vector4> buffer, ref Histogram<TBin>.Query query)
        {
            // Keep the complete output row and one neighborhood edge in separate parts of the same buffer.
            // The edge span is reused while the output row retains each pixel's original color and alpha.
            int sampleCount = radius + 1;
            int capacity = bounds.Width + sampleCount;
            Span<Vector4> colors = buffer[..bounds.Width];
            Span<Vector4> samples = buffer.Slice(bounds.Width, sampleCount);
            Span<int> indices = Histogram.GetIndices(buffer, capacity);
            Span<int> pixelIndices = indices[..bounds.Width];
            Span<int> sampleIndices = indices.Slice(bounds.Width, sampleCount);
            PixelOperations<TPixel> pixels = PixelOperations<TPixel>.Instance;
            InlineArray16<TData> resultStorage = default;
            Span<TData> results = resultStorage;
            bool rebuild = global is null;

            for (int y = rows.Min; y < rows.Max; y++)
            {
                ReadOnlySpan<TPixel> input = source.DangerousGetRowSpan(y).Slice(bounds.X, bounds.Width);
                pixels.ToUnassociatedScaledVector4(configuration, input, colors);
                mapping.Invoke(colors, weights, pixelIndices);
                int buffered = 0;
                for (int x = 0; x < colors.Length; x++)
                {
                    if (rebuild)
                    {
                        // Oil painting resolves equal counts by source order. Rebuild in that order for
                        // each neighborhood. Clear reuses the interval's allocated bin blocks.
                        query.Current.Clear();
                        for (int fy = 0; fy < sampleCount; fy++)
                        {
                            int sourceY = Math.Clamp(y + fy - radius, 0, bounds.Bottom - 1);
                            ReadOnlySpan<TPixel> row = source.DangerousGetRowSpan(sourceY);
                            int left = bounds.X + x - radius;
                            if (left >= 0)
                            {
                                // Most neighborhoods are contiguous. The existing bulk pixel API
                                // extracts their source colors without a per-pixel conversion loop.
                                pixels.ToUnassociatedScaledVector4(configuration, row.Slice(left, sampleCount), samples);
                            }
                            else
                            {
                                // The left edge repeats the first source pixel, retaining the existing border rule.
                                for (int fx = 0; fx < sampleCount; fx++)
                                {
                                    samples[fx] = row[Math.Max(0, left + fx)].ToUnassociatedScaledVector4();
                                }
                            }

                            mapping.Invoke(samples, weights, sampleIndices);
                            query.Current.Add(sampleIndices, samples, 1);
                        }

                        query.PrepareClippedQuery(clipLimit);
                    }

                    int index = pixelIndices[x];
                    results[buffered++] = operation.GetResult(ref query, colors[x], index);
                    if (buffered == results.Length || x + 1 == colors.Length)
                    {
                        Span<Vector4> block = colors.Slice(x - buffered + 1, buffered);
                        ApplyConsumerPixels<TBin, TData, TOperation>(block, results[..buffered], block, in operation);
                        buffered = 0;
                    }
                }

                pixels.FromUnassociatedScaledVector4Destructive(configuration, colors, target.DangerousGetRowSpan(y).Slice(bounds.X, bounds.Width));
            }
        }
    }
}
