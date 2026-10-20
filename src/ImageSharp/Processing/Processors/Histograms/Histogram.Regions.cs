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
/// Processes moving windows and image tiles through the shared histogram API.
/// </summary>
internal static partial class Histogram
{
    /// <summary>
    /// Runs sliding-window calculations on parallel column strips.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <typeparam name="TData">The per-pixel calculation data.</typeparam>
    /// <typeparam name="TOperation">The constrained calculation.</typeparam>
    /// <param name="configuration">The allocator, pixel conversion and memory budget configuration.</param>
    /// <param name="source">The pixels replaced by the completed result.</param>
    /// <param name="mapping">The shared measurement mapping.</param>
    /// <param name="weights">The RGB measurement coefficients.</param>
    /// <param name="clipHistogram">Whether queries cap and redistribute a copy of the counts.</param>
    /// <param name="clipLimit">The configured count limit.</param>
    /// <param name="windowWidth">The existing sample-window width.</param>
    /// <param name="operation">The same consumer contract used by other histogram regions.</param>
    private static void RunSlidingWindow<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        HistogramIndexMapping mapping,
        Vector3 weights,
        bool clipHistogram,
        int clipLimit,
        int windowWidth,
        in TOperation operation)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        int cacheHeight = Math.Max(1, (windowWidth / 2) * 2);
        int cachePadding = Math.Max(0, windowWidth - 1);
        long imageCacheBytes = (long)source.Width * cacheHeight * sizeof(int);
        long stripEdgeBytes = (long)cachePadding * cacheHeight * sizeof(int);
        int cacheLimit = Math.Min(configuration.WorkingBufferSizeHintInBytes, configuration.MemoryAllocator.GetBufferCapacityInBytes());

        // Each strip caches its output columns and the extra columns needed by its sampling window.
        // Empty windows need no extra columns, but still need storage for output colors and query indices.
        // The total cache stores the image columns once, plus these extra columns for each strip.
        // Limit the combined cache size to cacheLimit by reducing the maximum number of parallel strips.
        // This limit covers cached bin indices. The output frame and histogram bins have separate storage.
        // An index cache cannot supply original RGB samples to custom bins, so those bins use streamed edges.
        bool cacheRows = !TBin.RequiresSamples && imageCacheBytes + stripEdgeBytes <= cacheLimit;
        int maxDegreeOfParallelism = configuration.MaxDegreeOfParallelism;
        if (cacheRows && stripEdgeBytes != 0)
        {
            int cacheWorkers = (int)((cacheLimit - imageCacheBytes) / stripEdgeBytes);
            maxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? cacheWorkers : Math.Min(maxDegreeOfParallelism, cacheWorkers);
        }

        MemoryAllocator allocator = configuration.MemoryAllocator;
        using Buffer2D<TPixel> target = allocator.Allocate2D<TPixel>(source.Width, source.Height);
        WindowConsumer<TPixel, TBin, TData, TOperation> traversal = new(
            configuration, source, target, mapping, windowWidth, cachePadding, weights, cacheRows, clipHistogram, clipLimit, operation);

        // Transpose the scheduling rectangle so RowInterval partitions source columns. Each interval
        // reads unchanged source pixels and writes a disjoint strip of the single output buffer.
        Rectangle columns = new(0, 0, source.Height, source.Width);

        // The iterator chooses its batch count from the image area and these execution settings.
        ParallelExecutionSettings settings = new(maxDegreeOfParallelism, allocator);
        ParallelRowIterator.IterateRowIntervals(columns, in settings, in traversal);
        Buffer2D<TPixel>.SwapOrCopyContent(source, target);
    }

    /// <summary>
    /// Builds adjacent tile histograms and applies the consumer's interpolation calculation.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <typeparam name="TData">The per-pixel calculation data.</typeparam>
    /// <typeparam name="TOperation">The consumer's calculation.</typeparam>
    /// <param name="configuration">The allocator and pixel conversion configuration.</param>
    /// <param name="source">The source and destination pixels.</param>
    /// <param name="mapping">The shared measurement mapping.</param>
    /// <param name="weights">The RGB measurement coefficients.</param>
    /// <param name="clipHistogram">Whether tile counts are capped before cumulative calculation.</param>
    /// <param name="clipLimit">The configured count limit.</param>
    /// <param name="tileCount">The configured tile count on each axis.</param>
    /// <param name="operation">The common consumer calculation.</param>
    private static void RunTiles<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        HistogramIndexMapping mapping,
        Vector3 weights,
        bool clipHistogram,
        int clipLimit,
        int tileCount,
        in TOperation operation)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        int tileWidth = (int)MathF.Ceiling(source.Width / (float)tileCount);
        int tileHeight = (int)MathF.Ceiling(source.Height / (float)tileCount);

        // Only two adjacent tile rows are needed. This array owns references, while every sample
        // and bin buffer comes from the configured allocator. There is no independent memory pool.
        Histogram<TBin>[] tiles = new Histogram<TBin>[tileCount * 2];
        try
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = new Histogram<TBin>(configuration, mapping, tileWidth);
            }

            PrepareTileRow(
                configuration,
                source,
                tiles,
                0,
                0,
                tileWidth,
                tileHeight,
                tileCount,
                weights,
                clipHistogram,
                clipLimit,
                TOperation.UsesCumulativeCounts);

            for (int row = 0; row < tileCount; row++)
            {
                int topBank = row & 1;
                int bottomBank = topBank;
                if (row + 1 < tileCount)
                {
                    // Alternate the two retained rows. Replace the older histogram row only after its
                    // final output band is complete, then read the next tile row before writing into it.
                    bottomBank = topBank ^ 1;
                    PrepareTileRow(
                        configuration,
                        source,
                        tiles,
                        bottomBank,
                        row + 1,
                        tileWidth,
                        tileHeight,
                        tileCount,
                        weights,
                        clipHistogram,
                        clipLimit,
                        TOperation.UsesCumulativeCounts);
                }

                // This output band extends halfway into the bottom tile row. Its histogram was built
                // before any of those pixels changed. The next unread tile row lies beyond this band.
                int top = row == 0 ? 0 : (tileHeight / 2) + (row * tileHeight);
                int bottom = row + 1 == tileCount ? source.Height : Math.Min(source.Height, (tileHeight / 2) + ((row + 1) * tileHeight));
                if (top < bottom)
                {
                    Rectangle band = new(0, top, source.Width, bottom - top);
                    TileConsumer<TPixel, TBin, TData, TOperation> traversal = new(
                        configuration, source, tiles, mapping, weights, tileWidth, tileHeight, tileCount, row, topBank, bottomBank, operation);

                    ParallelRowIterator.IterateRowIntervals<TileConsumer<TPixel, TBin, TData, TOperation>, Vector4>(configuration, band, in traversal);
                }
            }
        }
        finally
        {
            // Construction can fail part-way through. Retain ownership here until every active
            // parallel band has returned, and return all successfully allocated buffers.
            foreach (Histogram<TBin> tile in tiles)
            {
                tile?.Dispose();
            }
        }
    }

    /// <summary>
    /// Reuses a tile row's sparse bins before any of its source pixels are changed.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="source">The unchanged rows to measure.</param>
    /// <param name="tiles">The two owned tile rows.</param>
    /// <param name="bank">The tile row to reuse.</param>
    /// <param name="row">The source tile-row index.</param>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <param name="count">The configured number of tiles per axis.</param>
    /// <param name="weights">The RGB measurement coefficients.</param>
    /// <param name="clipHistogram">Whether to cap tile counts.</param>
    /// <param name="clipLimit">The configured count limit.</param>
    /// <param name="prepareCdf">Whether the consumer uses cumulative counts.</param>
    private static void PrepareTileRow<TPixel, TBin>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        Histogram<TBin>[] tiles,
        int bank,
        int row,
        int width,
        int height,
        int count,
        Vector3 weights,
        bool clipHistogram,
        int clipLimit,
        bool prepareCdf)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
    {
        for (int tile = 0; tile < count; tile++)
        {
            Histogram<TBin>.Data data = tiles[(bank * count) + tile].GetData();
            data.Clear();

            // Ceiling division can leave a partial tile or an empty tile at the right edge.
            // Read only its actual source pixels while retaining the requested tile-grid positions.
            int left = tile * width;
            int length = Math.Min(width, Math.Max(0, source.Width - left));
            Span<Vector4> colors = data.Vectors[..length];
            for (int y = row * height; y < Math.Min(source.Height, (row + 1) * height); y++)
            {
                ReadOnlySpan<TPixel> input = source.DangerousGetRowSpan(y).Slice(Math.Min(left, source.Width), length);
                ReadOnlySpan<int> indices = data.Map(input, weights);
                data.Add(indices, colors, 1);
            }

            if (clipHistogram)
            {
                data.Clip(clipLimit);
            }

            if (prepareCdf)
            {
                data.PrepareCdf();
            }
        }
    }

    /// <summary>
    /// Moves the same histogram between adjacent output pixels and releases its cache after the invocation.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
    /// <typeparam name="TData">The per-pixel calculation data.</typeparam>
    /// <typeparam name="TOperation">The constrained pixel operation.</typeparam>
    /// <param name="configuration">The allocator and pixel conversion configuration.</param>
    /// <param name="source">The unchanged source until every strip is complete.</param>
    /// <param name="targetPixels">The shared output buffer. Each interval writes only its own columns.</param>
    /// <param name="mapping">The shared measurement mapping.</param>
    /// <param name="windowWidth">The sample-window width.</param>
    /// <param name="cachePadding">The nonnegative number of extra cached columns required by the window edges.</param>
    /// <param name="weights">The RGB measurement coefficients.</param>
    /// <param name="cacheRows">Whether the combined strip caches fit the working-buffer budget.</param>
    /// <param name="clipHistogram">Whether queries use a reusable clipped copy of the counts.</param>
    /// <param name="clipLimit">The configured count limit.</param>
    /// <param name="operation">The operation applied at each window position.</param>
    private readonly struct WindowConsumer<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        Buffer2D<TPixel> targetPixels,
        HistogramIndexMapping mapping,
        int windowWidth,
        int cachePadding,
        Vector3 weights,
        bool cacheRows,
        bool clipHistogram,
        int clipLimit,
        TOperation operation) : IRowIntervalOperation
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        /// <inheritdoc />
        public void Invoke(in RowInterval columns)
        {
            // Counts and conversion spans belong to this interval and remain reusable until it returns.
            // Other intervals read the same source but write disjoint columns of the shared output frame.
            int capacity = cacheRows ? columns.Max - columns.Min + cachePadding : Math.Max(1, windowWidth);
            using Histogram<TBin> histogram = new(configuration, mapping, capacity);
            using Histogram<TBin>? clipped = clipHistogram ? new(configuration, mapping, 0) : null;
            Histogram<TBin>.Query query = new(histogram.GetData());
            if (clipped is not null)
            {
                query.SetClippedStorage(clipped.GetData());
            }

            if (cacheRows)
            {
                this.ApplyCached(in columns, ref query);
            }
            else
            {
                this.ApplyStreamed(in columns, ref query);
            }
        }

        /// <summary>
        /// Retains mapped rows within the working-buffer budget and slides the histogram across rows.
        /// </summary>
        /// <param name="columns">The source columns owned by this interval.</param>
        /// <param name="query">The reusable count and calculation spans.</param>
        private void ApplyCached(in RowInterval columns, ref Histogram<TBin>.Query query)
        {
            ref Histogram<TBin>.Data histogram = ref query.Current;
            int radius = windowWidth / 2;
            int columnCount = columns.Max - columns.Min;
            int cacheLeft = columns.Min - radius;
            int cacheWidth = columnCount + cachePadding;

            // The existing sampling bounds use two half-heights, including for odd window widths.
            // Keep that sample count and the existing square-area normalization.
            int windowHeight = radius * 2;
            MemoryAllocator allocator = configuration.MemoryAllocator;

            // Cache only the source rows covered by the moving window. Each entry is a bin index,
            // not a color or a histogram. Replacing one departing row reuses its existing storage.
            // A minimum of one row also supports existing options that produce an empty sample window.
            // Transpose the cache, as resize does: each source column is one contiguous cached row.
            // Horizontal movement can then count its two column spans without gathering every sample.
            using Buffer2D<int> mappedColumns = allocator.Allocate2D<int>(Math.Max(1, windowHeight), cacheWidth);
            Span<int> indices = histogram.Indices;

            // Build the first window once. Mirrored top-edge samples use the same row conversion
            // and index mapping as every row subsequently introduced at the bottom.
            for (int slot = 0; slot < windowHeight; slot++)
            {
                this.MapSourceRow(source, slot - radius, cacheLeft, ref histogram);
                CacheMappedRow(mappedColumns, indices, slot);
                UpdateRow(indices, 0, windowWidth, ref histogram, 1);
            }

            InlineArray16<TData> resultStorage = default;
            Span<TData> results = resultStorage;
            PixelOperations<TPixel> pixels = PixelOperations<TPixel>.Instance;
            int firstSlot = 0;
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> sourceRow = source.DangerousGetRowSpan(y).Slice(columns.Min, columnCount);
                Span<TPixel> destinationRow = targetPixels.DangerousGetRowSpan(y).Slice(columns.Min, columnCount);
                Span<Vector4> colors = histogram.Vectors[..columnCount];
                pixels.ToUnassociatedScaledVector4(configuration, sourceRow, colors);
                histogram.Map(colors, weights);
                int buffered = 0;

                // Alternate direction so the last window of this row becomes the first window of
                // the next row after one vertical step. Counts never need a per-column rebuild.
                bool moveRight = (y & 1) == 0;
                int step = moveRight ? 1 : -1;
                int firstX = moveRight ? 0 : columnCount - 1;
                int endX = moveRight ? columnCount : -1;
                int departingOffset = moveRight ? -radius : windowWidth - radius - 1;
                int enteringOffset = moveRight ? windowWidth - radius : -radius - 1;

                for (int x = firstX; x != endX; x += step)
                {
                    int index = indices[x];
                    query.PrepareClippedQuery(clipLimit);

                    // Reverse traversal fills the block from its end so result order still matches increasing source columns.
                    int slot = moveRight ? buffered : results.Length - 1 - buffered;
                    results[slot] = operation.GetResult(ref query, colors[x], index);
                    buffered++;
                    if (buffered == results.Length || x + step == endX)
                    {
                        int firstColumn = moveRight ? x - buffered + 1 : x;
                        ReadOnlySpan<TData> blockResults = moveRight ? results[..buffered] : results[(results.Length - buffered)..];
                        Span<Vector4> block = colors.Slice(firstColumn, buffered);
                        ApplyConsumerPixels<TBin, TData, TOperation>(block, blockResults, block, in operation);

                        buffered = 0;
                    }

                    // Zero sample rows leave the histogram empty. Such windows have no edges to move.
                    if (x + step != endX && windowHeight > 0)
                    {
                        // The ring's physical row order does not affect integer counts. Both edges
                        // can use their cached spans directly, regardless of the current firstSlot.
                        ReadOnlySpan<int> departing = mappedColumns.DangerousGetRowSpan(x + departingOffset + radius);
                        ReadOnlySpan<int> entering = mappedColumns.DangerousGetRowSpan(x + enteringOffset + radius);
                        histogram.Add(departing[..windowHeight], ReadOnlySpan<Vector4>.Empty, -1);
                        histogram.Add(entering[..windowHeight], ReadOnlySpan<Vector4>.Empty, 1);
                    }
                }

                // Each result block now occupies its original columns. Pack the complete row once,
                // before edge mapping reuses the vector buffer for the next source row.
                pixels.FromUnassociatedScaledVector4Destructive(configuration, colors, destinationRow);

                // The oldest cached row occupies firstSlot. Remove its samples before replacing it,
                // then add the incoming row at the same horizontal position and advance the ring.
                // An empty sample window has no row to replace.
                if (y + 1 < source.Height && windowHeight > 0)
                {
                    int lastX = moveRight ? columnCount - 1 : 0;
                    UpdateCachedRow(mappedColumns, firstSlot, lastX, windowWidth, ref histogram);
                    this.MapSourceRow(source, y + radius, cacheLeft, ref histogram);
                    CacheMappedRow(mappedColumns, indices, firstSlot);

                    // The cache includes the left window edge, so lastX is already the first cached sample.
                    UpdateRow(indices, lastX, windowWidth, ref histogram, 1);

                    firstSlot++;
                    if (firstSlot == windowHeight)
                    {
                        firstSlot = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Slides down alternating columns using only two reusable edge buffers when a row cache cannot fit.
        /// </summary>
        /// <param name="columns">The source columns owned by this interval.</param>
        /// <param name="query">The reusable count and calculation spans.</param>
        private void ApplyStreamed(in RowInterval columns, ref Histogram<TBin>.Query query)
        {
            ref Histogram<TBin>.Data histogram = ref query.Current;
            int radius = windowWidth / 2;
            int windowHeight = radius * 2;

            // The histogram owns one reusable edge buffer and its count-query metadata.
            // Neither the histogram nor its sample buffer is rebuilt when the window turns.
            // The first column can be odd when the iterator splits the image. Its initial window
            // must start at the same image edge as that column's subsequent traversal.
            int firstRow = (columns.Min & 1) == 0 ? 0 : source.Height - 1;
            for (int y = firstRow - radius; y < firstRow + radius; y++)
            {
                this.UpdateSourceRow(source, y, columns.Min - radius, ref histogram, 1);
            }

            // Streaming columns need only a small color/result block; the edge-mapping buffer remains separate and reusable.
            InlineArray16<TData> resultStorage = default;
            InlineArray16<Vector4> colorStorage = default;
            Span<TData> results = resultStorage;
            Span<Vector4> colors = colorStorage;
            for (int x = columns.Min; x < columns.Max; x++)
            {
                int buffered = 0;
                bool moveDown = (x & 1) == 0;
                int step = moveDown ? 1 : -1;
                int firstY = moveDown ? 0 : source.Height - 1;
                int endY = moveDown ? source.Height : -1;
                int departingOffset = moveDown ? -radius : radius - 1;
                int enteringOffset = moveDown ? radius : -radius - 1;

                for (int y = firstY; y != endY; y += step)
                {
                    TPixel pixel = source.DangerousGetRowSpan(y)[x];
                    Vector4 vector = pixel.ToUnassociatedScaledVector4();
                    ReadOnlySpan<Vector4> sample = MemoryMarshal.CreateReadOnlySpan(ref vector, 1);
                    int index = histogram.Map(sample, weights)[0];
                    query.PrepareClippedQuery(clipLimit);
                    results[buffered] = operation.GetResult(ref query, vector, index);
                    colors[buffered++] = vector;
                    if (buffered == results.Length || y + step == endY)
                    {
                        Span<Vector4> block = colors[..buffered];
                        ApplyConsumerPixels<TBin, TData, TOperation>(block, results[..buffered], block, in operation);

                        // A column is not contiguous pixel storage. Scatter only the final packed pixels, in traversal order.
                        int firstOutputRow = y - ((buffered - 1) * step);
                        for (int i = 0; i < buffered; i++)
                        {
                            targetPixels.DangerousGetRowSpan(firstOutputRow + (i * step))[x] = TPixel.FromUnassociatedScaledVector4(block[i]);
                        }

                        buffered = 0;
                    }

                    // Zero sample rows leave the histogram empty. Do not add edges while advancing its output position.
                    if (y + step != endY && windowHeight > 0)
                    {
                        this.UpdateSourceRow(source, y + departingOffset, x - radius, ref histogram, -1);
                        this.UpdateSourceRow(source, y + enteringOffset, x - radius, ref histogram, 1);
                    }
                }

                if (x + 1 < columns.Max)
                {
                    // Keep the last window's counts when turning into the next column. Only its two
                    // vertical edges change; the remaining samples stay counted.
                    int lastY = moveDown ? source.Height - 1 : 0;
                    this.UpdateSourceColumn(source, x - radius, lastY - radius, windowHeight, ref histogram, -1);
                    this.UpdateSourceColumn(source, x - radius + windowWidth, lastY - radius, windowHeight, ref histogram, 1);
                }
            }
        }

        /// <summary>
        /// Converts and counts one horizontal source edge using the shared bulk APIs.
        /// </summary>
        /// <param name="source">The unchanged source pixels.</param>
        /// <param name="y">The logical source row.</param>
        /// <param name="left">The logical first column of the edge.</param>
        /// <param name="histogram">The moving window counts.</param>
        /// <param name="delta">One to add the edge, or minus one to remove it.</param>
        private void UpdateSourceRow(Buffer2D<TPixel> source, int y, int left, ref Histogram<TBin>.Data histogram, int delta)
        {
            Span<Vector4> vectors = histogram.Vectors;
            ReadOnlySpan<TPixel> row = source.DangerousGetRowSpan(GetSourceRow(y, source.Height));
            if (left >= 0 && left + vectors.Length <= source.Width)
            {
                ReadOnlySpan<int> mapped = histogram.Map(row.Slice(left, vectors.Length), weights);
                histogram.Add(mapped, vectors, delta);

                return;
            }
            else
            {
                for (int i = 0; i < vectors.Length; i++)
                {
                    vectors[i] = row[GetSourceColumn(left + i, source.Width)].ToUnassociatedScaledVector4();
                }
            }

            Span<int> indices = histogram.Map(vectors, weights);
            histogram.Add(indices, vectors[..indices.Length], delta);
        }

        /// <summary>
        /// Converts and counts one vertical edge when the streamed traversal advances to the next column.
        /// </summary>
        /// <param name="source">The unchanged source pixels.</param>
        /// <param name="x">The logical source column.</param>
        /// <param name="top">The logical first row of the edge.</param>
        /// <param name="length">The number of samples along the vertical edge.</param>
        /// <param name="histogram">The moving window counts.</param>
        /// <param name="delta">One to add the edge, or minus one to remove it.</param>
        private void UpdateSourceColumn(Buffer2D<TPixel> source, int x, int top, int length, ref Histogram<TBin>.Data histogram, int delta)
        {
            Span<Vector4> vectors = histogram.Vectors[..length];
            int column = GetSourceColumn(x, source.Width);
            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i] = source.DangerousGetRowSpan(GetSourceRow(top + i, source.Height))[column].ToUnassociatedScaledVector4();
            }

            Span<int> indices = histogram.Map(vectors, weights);
            histogram.Add(indices, vectors[..indices.Length], delta);
        }

        /// <summary>
        /// Resolves reflected samples above and below the source image.
        /// </summary>
        /// <param name="y">The logical source row.</param>
        /// <param name="height">The source height.</param>
        /// <returns>The source row containing the reflected sample.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetSourceRow(int y, int height)
        {
            if (y < 0)
            {
                return -y;
            }

            if (y >= height)
            {
                int distance = y - height;
                return height - distance - 1;
            }

            return y;
        }

        /// <summary>
        /// Converts one source row through the shared bulk pixel API and SIMD bin mapping.
        /// </summary>
        /// <param name="source">The unchanged source pixels.</param>
        /// <param name="y">The logical source row, including the mirrored border.</param>
        /// <param name="left">The first logical cache column, including the left window edge.</param>
        /// <param name="histogram">The owner of the reusable source conversion and index buffer.</param>
        private void MapSourceRow(Buffer2D<TPixel> source, int y, int left, ref Histogram<TBin>.Data histogram)
        {
            ReadOnlySpan<TPixel> row = source.DangerousGetRowSpan(GetSourceRow(y, source.Height));
            Span<Vector4> vectors = histogram.Vectors;
            int start = Math.Max(0, -left);
            int end = Math.Min(vectors.Length, source.Width - left);

            // Convert the contiguous source span in bulk. Only edge samples need individual reflection
            // or clamping; storing them in the cache makes every later window edge contiguous.
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, row.Slice(left + start, end - start), vectors[start..end]);
            for (int i = 0; i < start; i++)
            {
                vectors[i] = row[GetSourceColumn(left + i, source.Width)].ToUnassociatedScaledVector4();
            }

            for (int i = end; i < vectors.Length; i++)
            {
                vectors[i] = row[GetSourceColumn(left + i, source.Width)].ToUnassociatedScaledVector4();
            }

            histogram.Map(vectors, weights);
        }

        /// <summary>
        /// Adds or removes a horizontal window edge using cached indices and the shared count operation.
        /// </summary>
        /// <param name="row">A mapped source strip with its complete left and right window edges.</param>
        /// <param name="left">The logical first column of the window.</param>
        /// <param name="width">The number of edge samples.</param>
        /// <param name="histogram">The moving window counts.</param>
        /// <param name="delta">One to add samples, or minus one to remove them.</param>
        private static void UpdateRow(ReadOnlySpan<int> row, int left, int width, ref Histogram<TBin>.Data histogram, int delta)
            => histogram.Add(row.Slice(left, width), ReadOnlySpan<Vector4>.Empty, delta);

        /// <summary>
        /// Stores a newly mapped source row in the transposed ring cache.
        /// </summary>
        /// <param name="columns">The transposed cache, with one contiguous buffer row per source column.</param>
        /// <param name="indices">The new source row's mapped indices.</param>
        /// <param name="slot">The ring slot vacated by the departing source row.</param>
        private static void CacheMappedRow(Buffer2D<int> columns, ReadOnlySpan<int> indices, int slot)
        {
            // Scatter each mapped row once. Each horizontal window step subsequently reads whole
            // contiguous columns, avoiding a gather and two temporary edge copies for every output pixel.
            for (int x = 0; x < indices.Length; x++)
            {
                columns.DangerousGetRowSpan(x)[slot] = indices[x];
            }
        }

        /// <summary>
        /// Removes the departing horizontal edge before its ring slot is replaced.
        /// </summary>
        /// <param name="columns">The transposed ring cache.</param>
        /// <param name="slot">The departing row's ring slot.</param>
        /// <param name="left">The window's logical first column.</param>
        /// <param name="width">The number of samples in the horizontal edge.</param>
        /// <param name="histogram">The moving window counts.</param>
        private static void UpdateCachedRow(Buffer2D<int> columns, int slot, int left, int width, ref Histogram<TBin>.Data histogram)
        {
            // Only the turn into the next output row needs a horizontal gather. All per-pixel
            // horizontal movement uses the contiguous cached columns directly.
            for (int i = 0; i < width; i++)
            {
                histogram.Add(columns.DangerousGetRowSpan(left + i)[slot], default, -1);
            }
        }

        /// <summary>
        /// Resolves the existing left reflection and right repeated-edge sample rules.
        /// </summary>
        /// <param name="x">The logical source column.</param>
        /// <param name="width">The source width.</param>
        /// <returns>The source column containing the extended-edge sample.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetSourceColumn(int x, int width) => x < 0 ? -x : Math.Min(x, width - 1);
    }

    /// <summary>
    /// Borrows adjacent tile spans before each segment and uses the common output loop.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <typeparam name="TBin">The bin arithmetic.</typeparam>
    /// <typeparam name="TData">The calculation data.</typeparam>
    /// <typeparam name="TOperation">The constrained calculation.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="source">The source and destination pixels.</param>
    /// <param name="tiles">The two retained tile rows.</param>
    /// <param name="mapping">The measurement mapping.</param>
    /// <param name="weights">The measurement coefficients.</param>
    /// <param name="tileWidth">The tile width.</param>
    /// <param name="tileHeight">The tile height.</param>
    /// <param name="tileCount">The configured number of tiles per axis.</param>
    /// <param name="tileY">The top tile row.</param>
    /// <param name="topBank">The retained top row.</param>
    /// <param name="bottomBank">The retained bottom row.</param>
    /// <param name="operation">The custom output calculation.</param>
    private readonly struct TileConsumer<TPixel, TBin, TData, TOperation>(
        Configuration configuration,
        Buffer2D<TPixel> source,
        Histogram<TBin>[] tiles,
        HistogramIndexMapping mapping,
        Vector3 weights,
        int tileWidth,
        int tileHeight,
        int tileCount,
        int tileY,
        int topBank,
        int bottomBank,
        TOperation operation) : IRowIntervalOperation<Vector4>
        where TPixel : unmanaged, IPixel<TPixel>
        where TBin : unmanaged, IHistogramBin<TBin>
        where TData : unmanaged
        where TOperation : struct, IHistogramOperation<TBin, TData>
    {
        /// <inheritdoc />
        public int GetRequiredBufferLength(Rectangle rectangle) => Histogram.GetSampleBufferLength(rectangle.Width);

        /// <inheritdoc />
        public void Invoke(in RowInterval rows, Span<Vector4> buffer)
        {
            Span<Vector4> colors = buffer[..source.Width];
            Span<int> indices = Histogram.GetIndices(buffer, source.Width);
            PixelOperations<TPixel> pixels = PixelOperations<TPixel>.Instance;
            InlineArray16<TData> resultStorage = default;
            Span<TData> results = resultStorage;
            int halfWidth = tileWidth / 2;
            int halfHeight = tileHeight / 2;

            // A one-pixel tile has no positions between its centers. Its offset is zero;
            // use one as the divisor so the weight is zero rather than NaN.
            int horizontalIntervals = Math.Max(1, tileWidth - 1);
            int verticalIntervals = Math.Max(1, tileHeight - 1);
            for (int y = rows.Min; y < rows.Max; y++)
            {
                Span<TPixel> row = source.DangerousGetRowSpan(y);
                pixels.ToUnassociatedScaledVector4(configuration, row, colors);
                mapping.Invoke(colors, weights, indices);

                // Before the first tile center, repeat the first histogram. At the bottom edge both
                // banks refer to the same tile row, so the vertical interpolation weight is also zero.
                float ty = y < halfHeight || topBank == bottomBank ? 0F : (y - halfHeight - (tileY * tileHeight)) / (float)verticalIntervals;
                int x = 0;
                while (x < source.Width)
                {
                    // This segment uses one fixed pair of tile columns. Borrow their four histogram
                    // spans once, then change only the interpolation weights while visiting its pixels.
                    int left = x < halfWidth ? 0 : Math.Min(tileCount - 1, (x - halfWidth) / tileWidth);
                    int right = x < halfWidth ? left : Math.Min(tileCount - 1, left + 1);
                    int end = x < halfWidth ? halfWidth : Math.Min(source.Width, halfWidth + ((left + 1) * tileWidth));
                    Histogram<TBin>.Query query = new(
                        tiles[(topBank * tileCount) + left].GetData(),
                        tiles[(topBank * tileCount) + right].GetData(),
                        tiles[(bottomBank * tileCount) + left].GetData(),
                        tiles[(bottomBank * tileCount) + right].GetData());

                    query.VerticalWeight = ty;
                    int buffered = 0;
                    for (; x < end; x++)
                    {
                        query.HorizontalWeight = left == right ? 0F : (x - halfWidth - (left * tileWidth)) / (float)horizontalIntervals;
                        results[buffered++] = operation.GetResult(ref query, colors[x], indices[x]);
                        if (buffered == results.Length || x + 1 == end)
                        {
                            Span<Vector4> block = colors.Slice(x - buffered + 1, buffered);
                            ApplyConsumerPixels<TBin, TData, TOperation>(block, results[..buffered], block, in operation);
                            buffered = 0;
                        }
                    }
                }

                pixels.FromUnassociatedScaledVector4Destructive(configuration, colors, row);
            }
        }
    }
}
