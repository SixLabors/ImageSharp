// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Histograms;

/// <summary>
/// Owns sparse bin blocks and reusable sample storage. Processing borrows spans for the owner's lifetime.
/// </summary>
/// <typeparam name="TBin">The consumer's bin arithmetic.</typeparam>
/// <remarks>
/// <para>
/// Both SDR and extended mappings use pages of 128 bins. The default extended mapping has 70,913 logical bins,
/// or 555 pages. A directory maps each used logical page to a physical slot. First use assigns the next slot,
/// so a sample with a high bin index does not require storage for all preceding bins.
/// </para>
/// <para>
/// Physical blocks hold 1, 2, 4 and then twice as many pages, with the final block capped to the logical page count.
/// Adding a block does not copy existing bins. Blocks can contain unused capacity and remain allocated until
/// disposal. Reset clears the active-page directory and totals; pages are cleared when they are used again.
/// The configured allocator supplies all buffers. Sample vectors, indices, the directory and page totals share
/// one working allocation. Processing retains spans so bin lookups do not repeatedly access Memory.Span.
/// </para>
/// <para>
/// Each update changes the selected bin and its page total. A cumulative query sums preceding page totals
/// and the required bins within the selected page, then subtracts the first occupied bin's count.
/// A fixed histogram can prepare these cumulative values once. A moving window retains raw counts so it can
/// subtract departing samples. Histogram count clipping caps and redistributes counts; it does not clip RGB.
/// </para>
/// </remarks>
internal sealed class Histogram<TBin> : IDisposable
    where TBin : unmanaged, IHistogramBin<TBin>
{
    private readonly Configuration configuration;
    private readonly HistogramIndexMapping mapping;
    private readonly int capacity;
    private readonly IMemoryOwner<byte> memory;

    // An int bin count needs at most 2^24 pages of 128 bins. Blocks hold 1, 2, 4, ... pages;
    // blocks 0 through 23 hold 2^24 - 1 pages, so block 24 covers the last possible page.
    // These entries retain buffer ownership. Bin storage is allocated only when a block is first used.
    private InlineArray25<Block> blocks;

    private int blockCount;
    private bool cumulative;
    private int minimum;

    /// <summary>
    /// Initializes a new instance of the <see cref="Histogram{TBin}"/> class.
    /// </summary>
    /// <param name="configuration">The configured allocator and pixel conversion settings.</param>
    /// <param name="mapping">The shared measurement mapping.</param>
    /// <param name="capacity">The maximum number of samples to convert together.</param>
    public Histogram(Configuration configuration, HistogramIndexMapping mapping, int capacity)
    {
        this.configuration = configuration;
        this.mapping = mapping;
        this.capacity = capacity;
        int sampleBytes = Histogram.GetSampleByteLength(capacity);
        int pageCount = Data.GetPageCount(mapping.BinCount);
        int directoryBytes = Data.GetRequiredBufferLength(mapping.BinCount);

        // One rent holds the sample vectors, their integer indices, alignment padding, page directory,
        // and page totals. Bin blocks are rented separately when samples first use their pages.
        this.memory = configuration.MemoryAllocator.Allocate<byte>(sampleBytes + directoryBytes + (pageCount * sizeof(int)));

        // Only bookkeeping requires initial clearing. Sample buffers are overwritten before use;
        // each bin page is cleared when it first becomes active.
        this.memory.Memory.Span[sampleBytes..].Clear();
    }

    /// <summary>
    /// Gets the existing owner used to serialize merges without allocating a separate lock.
    /// </summary>
    public object SyncRoot => this;

    /// <summary>
    /// Borrows sample and bin spans before processing starts.
    /// </summary>
    /// <returns>The borrowed data, valid until this owner is disposed.</returns>
    public Data GetData() => new(this, this.memory.Memory.Span);

    /// <summary>
    /// Returns every allocated block and the working buffer to the configured allocator.
    /// </summary>
    public void Dispose()
    {
        Span<Block> blocks = this.blocks;
        foreach (Block block in blocks)
        {
            block.Owner?.Dispose();
        }

        this.memory.Dispose();
    }

    /// <summary>
    /// Gets existing block memory for a new invocation to borrow once.
    /// </summary>
    /// <param name="index">The allocated block index.</param>
    /// <returns>The allocator-owned block.</returns>
    private Memory<TBin> GetBlockMemory(int index) => this.blocks[index].Memory;

    /// <summary>
    /// Reserves a page slot without copying existing bins.
    /// </summary>
    /// <param name="slot">The next consecutive physical page slot.</param>
    /// <param name="blockIndex">Receives the containing block index.</param>
    /// <param name="pageOffset">Receives the page's element offset within that block.</param>
    /// <returns>The containing block, which remains owned until disposal.</returns>
    private Memory<TBin> GetWritableBlock(int slot, out int blockIndex, out int pageOffset)
    {
        // Blocks hold 1, 2, 4, ... pages, starting at slots 0, 1, 3, ... .
        // New blocks leave all existing spans valid; no live bins are copied.
        blockIndex = BitOperations.Log2((uint)slot + 1);
        pageOffset = (slot - ((1 << blockIndex) - 1)) * Data.PageLength;
        ref Block block = ref this.blocks[blockIndex];
        if (block.Owner is null)
        {
            int blockPages = 1 << blockIndex;
            int pageCount = Math.Min(blockPages, Data.GetPageCount(this.mapping.BinCount) - (blockPages - 1));
            block.Owner = this.configuration.MemoryAllocator.Allocate<TBin>(pageCount * Data.PageLength);
            block.Memory = block.Owner.Memory;
            this.blockCount = blockIndex + 1;
        }

        return block.Memory;
    }

    /// <summary>
    /// Supplies histogram queries to the consumer's calculations. It owns no buffers.
    /// </summary>
    public ref struct Query
    {
        /// <summary>
        /// The current histogram and its retained spans. Window updates modify this same view.
        /// </summary>
        public Data Current;

        private Data right;
        private Data bottom;
        private Data diagonal;
        private Data clipped;
        private bool useClipped;

        /// <summary>
        /// Initializes a new instance of the <see cref="Query"/> struct for one histogram.
        /// </summary>
        /// <param name="current">The borrowed histogram data.</param>
        public Query(Data current)
        {
            this = default;
            this.Current = current;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Query"/> struct for adjacent tiles.
        /// </summary>
        /// <param name="current">The top-left tile.</param>
        /// <param name="right">The top-right tile.</param>
        /// <param name="bottom">The bottom-left tile.</param>
        /// <param name="diagonal">The bottom-right tile.</param>
        public Query(Data current, Data right, Data bottom, Data diagonal)
        {
            this = default;
            this.Current = current;
            this.right = right;
            this.bottom = bottom;
            this.diagonal = diagonal;
        }

        /// <summary>
        /// Gets or sets the horizontal interpolation weight between adjacent tile centers.
        /// </summary>
        public float HorizontalWeight { get; set; }

        /// <summary>
        /// Gets or sets the vertical interpolation weight between adjacent tile centers.
        /// </summary>
        public float VerticalWeight { get; set; }

        /// <summary>
        /// Gets a value indicating whether measurement indices represent signed extended magnitudes.
        /// </summary>
        public bool IsExtended => this.Current.IsExtended;

        /// <summary>
        /// Attaches reusable storage for clipped window queries.
        /// </summary>
        /// <param name="data">The independently owned scratch histogram.</param>
        public void SetClippedStorage(Data data)
        {
            this.clipped = data;
            this.useClipped = true;
        }

        /// <summary>
        /// Prepares a clipped query while retaining the original counts for later window movement.
        /// </summary>
        /// <param name="limit">The configured bin-count limit.</param>
        public void PrepareClippedQuery(int limit)
        {
            if (this.useClipped)
            {
                // Clipping redistributes counts. Apply it to the reusable copy so the moving window
                // can still subtract the original departing samples from Current.
                this.clipped.CopyFrom(ref this.Current);
                this.clipped.Clip(limit);
            }
        }

        /// <summary>
        /// Maps one scalar measurement through the current histogram's shared index mapping.
        /// </summary>
        /// <param name="value">The unchanged measurement.</param>
        /// <returns>The logical bin index.</returns>
        public int GetIndex(float value) => this.Current.GetIndex(value);

        /// <summary>
        /// Gets the cumulative count and first occupied count for one measurement.
        /// </summary>
        /// <param name="index">The mapped measurement.</param>
        /// <returns>The cumulative query result.</returns>
        public Histogram.Cdf GetCdf(int index) => this.useClipped ? this.clipped.GetCdf(index) : this.Current.GetCdf(index);

        /// <summary>
        /// Gets cumulative counts from the top-left, top-right, bottom-left and bottom-right tiles.
        /// </summary>
        /// <param name="index">The mapped measurement shared by all four tiles.</param>
        /// <param name="topLeft">Receives the top-left result.</param>
        /// <param name="topRight">Receives the top-right result.</param>
        /// <param name="bottomLeft">Receives the bottom-left result.</param>
        /// <param name="bottomRight">Receives the bottom-right result.</param>
        public void GetTileCdfs(int index, out Histogram.Cdf topLeft, out Histogram.Cdf topRight, out Histogram.Cdf bottomLeft, out Histogram.Cdf bottomRight)
        {
            topLeft = this.Current.GetCdf(index);
            topRight = this.right.GetCdf(index);
            bottomLeft = this.bottom.GetCdf(index);
            bottomRight = this.diagonal.GetCdf(index);
        }

        /// <summary>
        /// Gets the bin selected by the consumer's sample-count calculation.
        /// </summary>
        /// <returns>The most frequent bin, with its accumulated custom data.</returns>
        public TBin GetMostFrequent() => this.Current.GetMostFrequent();
    }

    /// <summary>
    /// Holds borrowed spans for sample conversion, bin arithmetic and queries.
    /// </summary>
    public ref struct Data
    {
        // A page holds 128 bins. The default extended mapping also assigns 128 bins per doubling interval,
        // but its interval boundaries need not align with page boundaries. Storage does not depend on that alignment.
        public const int PageShift = 7;
        public const int PageLength = 1 << PageShift;
        private const int PageMask = PageLength - 1;

        private readonly Span<int> activePages;
        private readonly Span<int> pageSlots;
        private readonly Span<int> pageOffsets;

        // Retain each block's span so bin access does not repeatedly call Memory.Span.
        // Span<TBin> cannot be an array element, so each span needs its own field in this ref struct.
        // The 25 fields cover the owner's maximum block count, derived above. Unallocated blocks have empty spans.
        private Span<TBin> block0;
        private Span<TBin> block1;
        private Span<TBin> block2;
        private Span<TBin> block3;
        private Span<TBin> block4;
        private Span<TBin> block5;
        private Span<TBin> block6;
        private Span<TBin> block7;
        private Span<TBin> block8;
        private Span<TBin> block9;
        private Span<TBin> block10;
        private Span<TBin> block11;
        private Span<TBin> block12;
        private Span<TBin> block13;
        private Span<TBin> block14;
        private Span<TBin> block15;
        private Span<TBin> block16;
        private Span<TBin> block17;
        private Span<TBin> block18;
        private Span<TBin> block19;
        private Span<TBin> block20;
        private Span<TBin> block21;
        private Span<TBin> block22;
        private Span<TBin> block23;
        private Span<TBin> block24;

        private readonly Histogram<TBin> owner;
        private readonly Span<Vector4> vectors;
        private readonly Span<int> indices;
        private readonly Span<int> pageTotals;
        private int selectedIndex;
        private int maximum;
        private bool selectionDirty;

        /// <summary>
        /// Initializes a new instance of the <see cref="Data"/> struct before sample traversal.
        /// </summary>
        /// <param name="owner">The histogram that retains all storage.</param>
        /// <param name="memory">The working span borrowed once by the owner.</param>
        public Data(Histogram<TBin> owner, Span<byte> memory)
        {
            this = default;
            this.owner = owner;

            // The vectors occupy the first bytes; their mapped integer indices follow them.
            // GetSampleByteLength includes padding before the page directory.
            int vectorBytes = owner.capacity * Unsafe.SizeOf<Vector4>();
            this.vectors = MemoryMarshal.Cast<byte, Vector4>(memory[..vectorBytes]);
            this.indices = MemoryMarshal.Cast<byte, int>(memory.Slice(vectorBytes, owner.capacity * sizeof(int)));
            int sampleBytes = Histogram.GetSampleByteLength(owner.capacity);
            int directoryBytes = Data.GetRequiredBufferLength(owner.mapping.BinCount);
            this.PageCount = GetPageCount(owner.mapping.BinCount);

            // Borrow typed bookkeeping spans once. Inner loops access these spans directly.
            Span<int> directory = MemoryMarshal.Cast<byte, int>(memory.Slice(sampleBytes, directoryBytes));

            // activePages[0] is the active-page count. Remaining entries list logical page numbers
            // for reset. pageSlots and pageOffsets locate each active page in the rented bin blocks.
            int activeLength = this.PageCount + 1;
            this.activePages = directory[..activeLength];
            this.pageSlots = directory.Slice(activeLength, this.PageCount);
            this.pageOffsets = directory.Slice(activeLength + this.PageCount, this.PageCount);

            // Retain each existing block span before entering a pixel loop.
            // A new block is borrowed on its first write and retained until this view ends.
            for (int i = 0; i < owner.blockCount; i++)
            {
                this.GetBlockSpan(i) = owner.GetBlockMemory(i).Span;
            }

            this.pageTotals = MemoryMarshal.Cast<byte, int>(memory[(sampleBytes + directoryBytes)..]);
            this.selectedIndex = 0;
            this.maximum = 0;
            this.selectionDirty = false;
        }

        /// <summary>
        /// Gets the reusable straight color samples.
        /// </summary>
        public Span<Vector4> Vectors => this.vectors;

        /// <summary>
        /// Gets the reusable mapped indices.
        /// </summary>
        public Span<int> Indices => this.indices;

        /// <summary>
        /// Gets the logical bin count.
        /// </summary>
        public int Length => this.owner.mapping.BinCount;

        /// <summary>
        /// Gets the number of logical pages.
        /// </summary>
        public int PageCount { get; }

        /// <summary>
        /// Gets a value indicating whether the shared mapping retains signed extended measurements.
        /// </summary>
        public bool IsExtended => this.owner.mapping.IsExtended;

        /// <summary>
        /// Maps one scalar measurement with the same rule as bulk conversion.
        /// </summary>
        /// <param name="value">The measurement.</param>
        /// <returns>The logical bin index.</returns>
        public int GetIndex(float value) => this.owner.mapping.Invoke(value);

        /// <summary>
        /// Converts source pixels and maps their measurements without changing source storage.
        /// </summary>
        /// <typeparam name="TPixel">The source pixel format.</typeparam>
        /// <param name="source">The unchanged pixels.</param>
        /// <param name="weights">The RGB measurement coefficients.</param>
        /// <returns>The mapped indices, retained until the next mapping operation.</returns>
        public Span<int> Map<TPixel>(ReadOnlySpan<TPixel> source, Vector3 weights)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<Vector4> colors = this.vectors[..source.Length];
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.owner.configuration, source, colors);
            return this.Map(colors, weights);
        }

        /// <summary>
        /// Maps source colors without modifying them.
        /// </summary>
        /// <param name="source">The unchanged straight colors.</param>
        /// <param name="weights">The RGB measurement coefficients.</param>
        /// <returns>The reusable mapped indices.</returns>
        public Span<int> Map(ReadOnlySpan<Vector4> source, Vector3 weights)
        {
            Span<int> destination = this.indices[..source.Length];
            this.owner.mapping.Invoke(source, weights, destination);
            return destination;
        }

        /// <summary>
        /// Adds or removes one sample through the consumer's bin arithmetic.
        /// </summary>
        /// <param name="index">The mapped bin index.</param>
        /// <param name="sample">The unchanged source color when the bin requires it.</param>
        /// <param name="delta">The signed sample count change.</param>
        public void Add(int index, Vector4 sample, int delta)
        {
            int count = this.GetBin(index).Add(sample, delta);
            this.pageTotals[index >> Data.PageShift] += delta;
            if (TBin.TrackMaximum)
            {
                if (count > this.maximum)
                {
                    this.maximum = count;
                    this.selectedIndex = index;
                }

                this.selectionDirty |= delta < 0 && index == this.selectedIndex;
            }
        }

        /// <summary>
        /// Updates a run of mapped samples while retaining each page span through its inner loop.
        /// </summary>
        /// <param name="mapped">The mapped indices.</param>
        /// <param name="samples">Matching source colors, or empty for count-only bins.</param>
        /// <param name="delta">The signed sample count change.</param>
        public void Add(ReadOnlySpan<int> mapped, ReadOnlySpan<Vector4> samples, int delta)
        {
            int i = 0;
            while (i < mapped.Length)
            {
                // Resolve the backing span once for consecutive samples in this page. Keep sample
                // order unchanged because custom bins can use that order to resolve equal counts.
                int pageIndex = mapped[i] >> Data.PageShift;
                Span<TBin> page = this.GetWritablePage(pageIndex);
                int start = i;
                do
                {
                    // Repeated indices retain source order. Count-only bins eliminate the color read
                    // through their static contract; color-sum bins receive the original sample.
                    Vector4 sample = TBin.RequiresSamples ? samples[i] : default;
                    int count = page[mapped[i] & (Data.PageLength - 1)].Add(sample, delta);
                    if (TBin.TrackMaximum)
                    {
                        // Equalization uses cumulative counts only. Its bin contract removes this
                        // selection work at JIT time; oil painting retains source-order tie handling.
                        if (count > this.maximum)
                        {
                            this.maximum = count;
                            this.selectedIndex = mapped[i];
                        }

                        this.selectionDirty |= delta < 0 && mapped[i] == this.selectedIndex;
                    }

                    i++;
                }
                while (i < mapped.Length && (mapped[i] >> Data.PageShift) == pageIndex);

                // Each sample changes the page total by delta. Page totals let a CDF query skip
                // complete pages without visiting their individual bins.
                this.pageTotals[pageIndex] += (i - start) * delta;
            }
        }

        /// <summary>
        /// Gets the most frequent bin. Tracked bins retain the first selection during accumulation.
        /// Untracked bins resolve equal counts by index when queried.
        /// </summary>
        /// <returns>The selected bin.</returns>
        public TBin GetMostFrequent()
        {
            if (!TBin.TrackMaximum || this.selectionDirty || this.maximum == 0)
            {
                // Removing samples or redistributing counts can invalidate the tracked maximum.
                // Recalculate it only when requested. Scanning in index order selects the first tied bin.
                this.maximum = 0;
                for (int p = 0; p < this.PageCount; p++)
                {
                    ReadOnlySpan<TBin> page = this.GetPage(p);
                    for (int i = 0; i < page.Length; i++)
                    {
                        if (page[i].Count > this.maximum)
                        {
                            this.maximum = page[i].Count;
                            this.selectedIndex = (p << Data.PageShift) + i;
                        }
                    }
                }

                this.selectionDirty = false;
            }

            return this.ReadBin(this.selectedIndex);
        }

        /// <summary>
        /// Queries cumulative counts without changing counts needed by a moving window.
        /// </summary>
        /// <param name="index">The mapped bin index.</param>
        /// <returns>The cumulative count with its first occupied count subtracted, and that first count.</returns>
        public Histogram.Cdf GetCdf(int index)
        {
            int lastPage = index >> Data.PageShift;
            ReadOnlySpan<TBin> page = this.GetPage(lastPage);
            if (this.owner.cumulative)
            {
                // Prepared pages store cumulative counts. A missing page inherits the complete
                // preceding pages, which also supports queries between occupied luminance pages.
                int cumulative = page.IsEmpty
                    ? Math.Max(0, TensorPrimitives.Sum<int>(this.pageTotals[..lastPage]) - this.owner.minimum)
                    : page[index & (Data.PageLength - 1)].Count;

                return new Histogram.Cdf(cumulative, this.owner.minimum);
            }

            // Sum complete logical pages before this index, then the prefix of its own page.
            // This reads the moving window's counts without replacing them with cumulative values.
            int countInPage = (index & (Data.PageLength - 1)) + 1;
            int sum = TensorPrimitives.Sum<int>(this.pageTotals[..lastPage]);
            if (!page.IsEmpty)
            {
                sum += TBin.SumCounts(page[..countInPage]);
            }

            if (sum == 0)
            {
                return default;
            }

            // Equalization subtracts the first occupied bin's count from the cumulative count.
            // The nonzero sum proves that an occupied page exists within this prefix.
            int firstPage = this.pageTotals[..(lastPage + 1)].IndexOfAnyExcept(0);
            ReadOnlySpan<TBin> first = this.GetPage(firstPage);
            int minimum = TBin.GetFirstCount(first);
            return new Histogram.Cdf(sum - minimum, minimum);
        }

        /// <summary>
        /// Prepares cumulative counts once when no further window updates are needed.
        /// </summary>
        public void PrepareCdf()
        {
            // Visit logical pages in measurement order, independently of their physical storage order.
            // Keep pageTotals unchanged so queries for missing pages can still sum preceding samples.
            int sum = 0;
            int minimum = 0;
            for (int p = 0; p < this.PageCount; p++)
            {
                Span<TBin> page = this.GetPage(p);
                for (int i = 0; i < page.Length; i++)
                {
                    sum += page[i].Count;
                    if (minimum == 0 && sum != 0)
                    {
                        minimum = sum;
                    }

                    page[i].Count = sum - minimum;
                }
            }

            this.owner.minimum = minimum;
            this.owner.cumulative = true;
        }

        /// <summary>
        /// Merges independently accumulated bins. The caller serializes access to this owner.
        /// </summary>
        /// <param name="source">The unchanged interval histogram.</param>
        public void Merge(ref Data source)
        {
            for (int p = 0; p < source.PageCount; p++)
            {
                ReadOnlySpan<TBin> page = source.GetPage(p);
                if (!page.IsEmpty)
                {
                    Span<TBin> target = this.GetWritablePage(p);
                    for (int i = 0; i < page.Length; i++)
                    {
                        target[i].Merge(in page[i]);
                    }

                    this.pageTotals[p] += source.pageTotals[p];
                }
            }

            this.selectionDirty = true;
        }

        /// <summary>
        /// Copies original counts into reusable query storage before clipping.
        /// </summary>
        /// <param name="source">The unchanged original counts.</param>
        public void CopyFrom(ref Data source)
        {
            this.Clear();
            for (int p = 0; p < source.PageCount; p++)
            {
                ReadOnlySpan<TBin> page = source.GetPage(p);
                if (!page.IsEmpty)
                {
                    page.CopyTo(this.GetWritablePage(p));
                    this.pageTotals[p] = source.pageTotals[p];
                }
            }
        }

        /// <summary>
        /// Caps bin counts and redistributes excess counts across the logical bin range.
        /// </summary>
        /// <param name="limit">The configured count limit.</param>
        public void Clip(int limit)
        {
            int excess = 0;
            for (int p = 0; p < this.PageCount; p++)
            {
                Span<TBin> page = this.GetPage(p);
                for (int i = 0; i < page.Length; i++)
                {
                    if (page[i].Count > limit)
                    {
                        excess += page[i].Count - limit;
                        page[i].Count = limit;
                    }
                }
            }

            int perBin = excess / this.Length;
            if (perBin != 0)
            {
                // Redistribution assigns counts to unused logical bins too. They require storage
                // once their count becomes nonzero; omitting them changes the existing algorithm.
                for (int p = 0; p < this.PageCount; p++)
                {
                    Span<TBin> page = this.GetWritablePage(p);
                    for (int i = 0; i < page.Length; i++)
                    {
                        page[i].Count += perBin;
                    }
                }
            }

            int residual = excess - (perBin * this.Length);
            if (residual != 0)
            {
                // Spread the remainder over the logical bin range, including gaps between allocated
                // pages. Physical page order must not affect which bins receive these counts.
                int step = Math.Max(this.Length / residual, 1);
                for (int i = 0; i < this.Length && residual > 0; i += step, residual--)
                {
                    this.GetBin(i).Count++;
                }
            }

            // Capping and redistribution both change page totals. Rebuild them before the next CDF query.
            for (int p = 0; p < this.PageCount; p++)
            {
                this.pageTotals[p] = TBin.SumCounts(this.GetPage(p));
            }

            this.selectionDirty = true;
        }

        /// <summary>
        /// Clears logical bins and totals while retaining every allocated block for reuse.
        /// </summary>
        public void Clear()
        {
            this.ClearPages();
            this.pageTotals.Clear();
            this.owner.cumulative = false;
            this.owner.minimum = 0;
            this.selectedIndex = 0;
            this.maximum = 0;
            this.selectionDirty = false;
        }

        /// <summary>
        /// Gets the storage required for page slots and the reset list.
        /// </summary>
        /// <param name="length">The logical bin count.</param>
        /// <returns>The backing-buffer size in bytes.</returns>
        public static int GetRequiredBufferLength(int length) => ((GetPageCount(length) * 3) + 1) * sizeof(int);

        /// <summary>
        /// Gets the page-directory length required for a logical bin count.
        /// </summary>
        /// <param name="length">The logical bin count.</param>
        /// <returns>The number of page entries.</returns>
        public static int GetPageCount(int length)
        {
            // Bin counts are nonnegative ints. Unsigned arithmetic leaves enough room for the
            // rounding addition, even at int.MaxValue. The resulting page count still fits in an int.
            return (int)Numerics.DivideCeil((uint)length, PageLength);
        }

        /// <summary>
        /// Gets or creates one writable bin.
        /// </summary>
        /// <param name="index">A validated mapping result.</param>
        /// <returns>The caller's accumulator for that bin.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TBin GetBin(int index)
        {
            return ref this.GetWritablePage(index >> PageShift)[index & PageMask];
        }

        /// <summary>
        /// Gets a page slice and clears it on its first write after a reset.
        /// </summary>
        /// <param name="pageIndex">The logical page index.</param>
        /// <returns>The page span, valid until the histogram releases its backing blocks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<TBin> GetWritablePage(int pageIndex)
        {
            Span<int> slots = this.pageSlots;
            int storedSlot = slots[pageIndex];
            Span<TBin> page;
            if (storedSlot == 0)
            {
                // Assign consecutive physical slots only to used virtual pages. Reset reuses these
                // slots across neighborhoods instead of accumulating every page seen in the image.
                Span<int> active = this.activePages;
                int count = active[0];
                Memory<TBin> memory = this.owner.GetWritableBlock(count, out int blockIndex, out int pageOffset);
                ref Span<TBin> block = ref this.GetBlockSpan(blockIndex);
                if (block.IsEmpty)
                {
                    // Resolve each newly allocated block once. Reset retains this span and its owner.
                    block = memory.Span;
                }

                page = block.Slice(pageOffset, PageLength);
                page.Clear();

                // Zero marks an inactive page, so store the block index plus one. The offset identifies
                // the page within that block. Later reads use these two values without searching the blocks.
                slots[pageIndex] = blockIndex + 1;
                this.pageOffsets[pageIndex] = pageOffset;

                // Retain only active logical page numbers for reset. The next neighborhood can assign
                // these same physical slots to different logical pages without growing the storage.
                active[count + 1] = pageIndex;
                active[0] = count + 1;
            }
            else
            {
                page = this.GetBlockSpan(storedSlot - 1).Slice(this.pageOffsets[pageIndex], PageLength);
            }

            // The last physical page can contain padding bins. Exclude them from queries and clipping.
            return page[..Math.Min(PageLength, this.Length - (pageIndex << PageShift))];
        }

        /// <summary>
        /// Reads an active page. An inactive page represents zero accumulators.
        /// </summary>
        /// <param name="pageIndex">The logical page number.</param>
        /// <returns>The active page, or an empty span when it has no active slot.</returns>
        public Span<TBin> GetPage(int pageIndex)
        {
            int storedSlot = this.pageSlots[pageIndex];
            if (storedSlot == 0)
            {
                return Span<TBin>.Empty;
            }

            Span<TBin> page = this.GetBlockSpan(storedSlot - 1).Slice(this.pageOffsets[pageIndex], PageLength);

            // The last physical page can contain padding bins. Exclude them from queries and clipping.
            return page[..Math.Min(PageLength, this.Length - (pageIndex << PageShift))];
        }

        /// <summary>
        /// Reads a bin without creating a page or modifying page ownership.
        /// </summary>
        /// <param name="index">The logical bin index.</param>
        /// <returns>The accumulator, or zero-initialized storage for an absent page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TBin ReadBin(int index)
        {
            Span<TBin> page = this.GetPage(index >> PageShift);
            return page.IsEmpty ? default : page[index & PageMask];
        }

        /// <summary>
        /// Resets active pages without releasing their shared backing storage.
        /// </summary>
        private void ClearPages()
        {
            // Inactive pages read as zero. Defer clearing their values until the next write,
            // so a later neighborhood does not clear pages that it never uses.
            Span<int> active = this.activePages;
            Span<int> slots = this.pageSlots;
            for (int i = 1; i <= active[0]; i++)
            {
                slots[active[i]] = 0;
            }

            active[0] = 0;
        }

        /// <summary>
        /// Gets a retained block span without accessing its memory owner.
        /// </summary>
        /// <param name="index">The block index produced by the owner's binary slot mapping.</param>
        /// <returns>A reference to the span retained for that block.</returns>
        [UnscopedRef]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Span<TBin> GetBlockSpan(int index)
        {
            // The owner assigns page slots consecutively. Its page-count limit bounds block indices to 0 through 24.
            // The final arm selects block 24; callers cannot supply an index outside the owner's block table.
            switch (index)
            {
                case 0: return ref this.block0;
                case 1: return ref this.block1;
                case 2: return ref this.block2;
                case 3: return ref this.block3;
                case 4: return ref this.block4;
                case 5: return ref this.block5;
                case 6: return ref this.block6;
                case 7: return ref this.block7;
                case 8: return ref this.block8;
                case 9: return ref this.block9;
                case 10: return ref this.block10;
                case 11: return ref this.block11;
                case 12: return ref this.block12;
                case 13: return ref this.block13;
                case 14: return ref this.block14;
                case 15: return ref this.block15;
                case 16: return ref this.block16;
                case 17: return ref this.block17;
                case 18: return ref this.block18;
                case 19: return ref this.block19;
                case 20: return ref this.block20;
                case 21: return ref this.block21;
                case 22: return ref this.block22;
                case 23: return ref this.block23;
                default: return ref this.block24;
            }
        }
    }

    /// <summary>
    /// Retains ownership of a shared bin block.
    /// </summary>
    private struct Block
    {
        /// <summary>
        /// The allocator-owned storage. Unallocated entries remain default.
        /// </summary>
        public IMemoryOwner<TBin> Owner;

        /// <summary>
        /// The block memory retained for span borrowing.
        /// </summary>
        public Memory<TBin> Memory;
    }
}
