// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Holds one frame at a series of halved resolutions, for dense flow estimation to walk.
/// </summary>
/// <remarks>
/// Every level is eight bits deep, whatever the coded depth, and carries a replicated border so that
/// the flow kernels may read past a level's edge without a boundary test. Reference:
/// aom_alloc_pyramid(), fill_pyramid() and fill_border().
/// </remarks>
internal sealed class Av1ImagePyramid : IDisposable
{
    /// <summary>
    /// The samples of replicated border held on each side of every level.
    /// </summary>
    /// <remarks>Reference: PYRAMID_PADDING.</remarks>
    public const int Padding = 16;

    /// <summary>
    /// The byte alignment of the first coded sample of every row of every level.
    /// </summary>
    /// <remarks>Reference: PYRAMID_ALIGNMENT.</remarks>
    private const int Alignment = 32;

    /// <summary>
    /// The base-two logarithm of the smallest level extent the reference will produce.
    /// </summary>
    /// <remarks>Reference: MIN_PYRAMID_SIZE_LOG2.</remarks>
    private const int MinimumSizeLog2 = 3;

    private readonly MemoryAllocator allocator;
    private readonly Level[] levels;
    private IMemoryOwner<byte>? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ImagePyramid"/> class.
    /// </summary>
    /// <param name="allocator">The allocator of the level storage.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <remarks>
    /// Storage for every level the frame size allows is reserved once, because the reference keeps one
    /// allocation for the whole pyramid and a frame's pyramid is rebuilt for every reference it is
    /// compared against.
    /// </remarks>
    public Av1ImagePyramid(MemoryAllocator allocator, int width, int height)
    {
        this.allocator = allocator;
        this.LevelCount = GetMaximumLevelCount(width, height);
        this.levels = new Level[this.LevelCount];

        // Each level is laid out with its border, and its stride is aligned so that the first coded
        // sample of every row shares the alignment of the first coded sample of the level.
        int length = 0;
        for (int level = 0; level < this.LevelCount; level++)
        {
            int levelWidth = width >> level;
            int levelHeight = height >> level;
            int stride = (levelWidth + (2 * Padding) + Alignment - 1) & ~(Alignment - 1);
            this.levels[level] = new Level(levelWidth, levelHeight, stride, length);
            length += stride * (levelHeight + (2 * Padding));
        }

        this.owner = allocator.Allocate<byte>(length);
        this.FilledLevelCount = 0;
    }

    /// <summary>
    /// Gets the number of levels the frame size allows.
    /// </summary>
    public int LevelCount { get; }

    /// <summary>
    /// Gets the number of levels holding samples.
    /// </summary>
    public int FilledLevelCount { get; private set; }

    /// <summary>
    /// Gets the number of levels a frame of one size allows.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <returns>The level count, which is at least one.</returns>
    /// <remarks>
    /// Halving stops once the shorter side reaches the smallest useful extent, so the count is the
    /// position of the shorter side's most significant bit less that extent's logarithm.
    /// Reference: aom_alloc_pyramid().
    /// </remarks>
    public static int GetMaximumLevelCount(int width, int height)
    {
        int shorter = Math.Min(width, height);
        int mostSignificantBit = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)shorter);
        return Math.Max(mostSignificantBit - MinimumSizeLog2, 1);
    }

    /// <summary>
    /// Gets the geometry of one level.
    /// </summary>
    /// <param name="level">The level index, where zero is the frame resolution.</param>
    /// <returns>The level geometry.</returns>
    public Level GetLevel(int level) => this.levels[level];

    /// <summary>
    /// Gets the storage of one level, including its border.
    /// </summary>
    /// <param name="level">The level index, where zero is the frame resolution.</param>
    /// <returns>The level storage, indexed by the level's stride.</returns>
    /// <remarks>
    /// The span covers the border as well as the coded samples, because a span cannot express a
    /// negative index. A caller reads the sample at column x of row y as
    /// <c>Origin + (y * Stride) + x</c>, where x and y may both be negative down to the padding.
    /// </remarks>
    public Span<byte> GetSamples(int level) => this.owner!.Memory.Span[this.levels[level].Offset..];

    /// <summary>
    /// Fills the requested number of levels from one high-bit-depth frame.
    /// </summary>
    /// <param name="source">The frame samples.</param>
    /// <param name="sourceStride">The frame row stride.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="requestedLevels">The number of levels to fill.</param>
    /// <returns>The number of levels filled.</returns>
    /// <remarks>
    /// A pyramid is eight bits deep whatever the frame is, so the low bits of each sample are
    /// dropped as it is copied. Reference: the high-bit-depth branch of fill_pyramid().
    /// </remarks>
    public int Fill(ReadOnlySpan<ushort> source, int sourceStride, int bitDepth, int requestedLevels)
    {
        int count = Math.Min(requestedLevels, this.LevelCount);
        if (this.FilledLevelCount >= count)
        {
            return count;
        }

        if (this.FilledLevelCount == 0)
        {
            Span<byte> storage = this.owner!.Memory.Span;
            Level first = this.levels[0];
            int shift = bitDepth - 8;
            for (int row = 0; row < first.Height; row++)
            {
                Narrow(
                    source.Slice(row * sourceStride, first.Width),
                    storage.Slice(first.Offset + first.Origin + (row * first.Stride), first.Width),
                    shift);
            }

            FillBorder(storage, first);
            this.FilledLevelCount = 1;
        }

        // Every level below the first is halved from the level above it, which is already eight bits
        // deep, so the rest of the work is the same at either coded depth.
        return this.Fill(ReadOnlySpan<byte>.Empty, 0, count);
    }

    /// <summary>
    /// Fills the requested number of levels from one eight-bit frame.
    /// </summary>
    /// <param name="source">The frame samples.</param>
    /// <param name="sourceStride">The frame row stride.</param>
    /// <param name="requestedLevels">The number of levels to fill.</param>
    /// <returns>The number of levels filled.</returns>
    public int Fill(ReadOnlySpan<byte> source, int sourceStride, int requestedLevels)
    {
        int count = Math.Min(requestedLevels, this.LevelCount);
        if (this.FilledLevelCount >= count)
        {
            return count;
        }

        Span<byte> storage = this.owner!.Memory.Span;
        if (this.FilledLevelCount == 0)
        {
            // The reference points its first level at the frame buffer when the frame is eight bits
            // deep, and borrows that buffer's border. This port copies instead, so that a pyramid owns
            // every sample it reads and a frame buffer is never given a second border regime. The
            // samples are the same either way.
            Level first = this.levels[0];
            for (int row = 0; row < first.Height; row++)
            {
                source.Slice(row * sourceStride, first.Width)
                    .CopyTo(storage.Slice(first.Offset + first.Origin + (row * first.Stride), first.Width));
            }

            FillBorder(storage, first);
            this.FilledLevelCount = 1;
        }

        for (int level = this.FilledLevelCount; level < count; level++)
        {
            Level previous = this.levels[level - 1];
            Level current = this.levels[level];

            // The halving reads exactly twice the current extent, which clips the last row or column
            // off a previous level of odd extent. Keeping the ratio at exactly two is what lets the
            // flow field be rescaled by a shift when it moves between levels.
            Av1PlaneDownsampler.Halve(
                this.allocator,
                storage[(previous.Offset + previous.Origin)..],
                previous.Stride,
                current.Width << 1,
                current.Height << 1,
                storage[(current.Offset + current.Origin)..],
                current.Stride);

            FillBorder(storage, current);
        }

        this.FilledLevelCount = count;
        return count;
    }

    /// <summary>
    /// Drops the low bits of one row of samples and stores the result as bytes.
    /// </summary>
    /// <param name="source">The row to read.</param>
    /// <param name="destination">The row to write.</param>
    /// <param name="shift">The low bits to drop.</param>
    internal static void Narrow(ReadOnlySpan<ushort> source, Span<byte> destination, int shift)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = source.Length;
        int x = 0;

        // Two source vectors make one destination vector, because a sample halves in width as it is
        // narrowed. The widest stage that the hardware has and the row can fill is taken first.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= length - Vector512<byte>.Count; x += Vector512<byte>.Count)
            {
                Vector512<ushort> lower = Vector512.LoadUnsafe(ref sourceBase, (nuint)x) >>> shift;
                Vector512<ushort> upper = Vector512.LoadUnsafe(ref sourceBase, (nuint)(x + Vector512<ushort>.Count)) >>> shift;
                Vector512.Narrow(lower, upper).StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= length - Vector256<byte>.Count; x += Vector256<byte>.Count)
            {
                Vector256<ushort> lower = Vector256.LoadUnsafe(ref sourceBase, (nuint)x) >>> shift;
                Vector256<ushort> upper = Vector256.LoadUnsafe(ref sourceBase, (nuint)(x + Vector256<ushort>.Count)) >>> shift;
                Vector256.Narrow(lower, upper).StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= length - Vector128<byte>.Count; x += Vector128<byte>.Count)
            {
                Vector128<ushort> lower = Vector128.LoadUnsafe(ref sourceBase, (nuint)x) >>> shift;
                Vector128<ushort> upper = Vector128.LoadUnsafe(ref sourceBase, (nuint)(x + Vector128<ushort>.Count)) >>> shift;
                Vector128.Narrow(lower, upper).StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        for (; x < length; x++)
        {
            Unsafe.Add(ref destinationBase, x) = (byte)(Unsafe.Add(ref sourceBase, x) >> shift);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
    }

    /// <summary>
    /// Replicates the outermost coded samples of one level into its border.
    /// </summary>
    /// <param name="storage">The pyramid storage.</param>
    /// <param name="level">The level geometry.</param>
    /// <remarks>
    /// The left and right borders repeat each row's end samples, and the top and bottom borders then
    /// repeat the first and last complete row, which carries the corners with them.
    /// Reference: fill_border().
    /// </remarks>
    private static void FillBorder(Span<byte> storage, Level level)
    {
        int paddedWidth = level.Width + (2 * Padding);
        int coded = level.Offset + level.Origin;
        for (int row = 0; row < level.Height; row++)
        {
            Span<byte> line = storage.Slice(coded + (row * level.Stride), level.Width);
            storage.Slice(coded + (row * level.Stride) - Padding, Padding).Fill(line[0]);
            storage.Slice(coded + (row * level.Stride) + level.Width, Padding).Fill(line[level.Width - 1]);
        }

        int firstRow = coded - Padding;
        int lastRow = coded + ((level.Height - 1) * level.Stride) - Padding;
        for (int row = 1; row <= Padding; row++)
        {
            storage.Slice(firstRow, paddedWidth).CopyTo(storage.Slice(firstRow - (row * level.Stride), paddedWidth));
            storage.Slice(lastRow, paddedWidth).CopyTo(storage.Slice(lastRow + (row * level.Stride), paddedWidth));
        }
    }

    /// <summary>
    /// Describes the geometry of one pyramid level.
    /// </summary>
    internal readonly struct Level
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Level"/> struct.
        /// </summary>
        /// <param name="width">The coded width.</param>
        /// <param name="height">The coded height.</param>
        /// <param name="stride">The row stride, including both borders.</param>
        /// <param name="offset">The index of the level's storage within the pyramid storage.</param>
        public Level(int width, int height, int stride, int offset)
        {
            this.Width = width;
            this.Height = height;
            this.Stride = stride;
            this.Offset = offset;
            this.Origin = (Padding * stride) + Padding;
        }

        /// <summary>
        /// Gets the coded width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the coded height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the row stride, including both borders.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets the index of the level's storage within the pyramid storage.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets the index of the first coded sample within the level's storage.
        /// </summary>
        /// <remarks>
        /// The border occupies the rows and columns before this index, so a caller may subtract up
        /// to the padding from it on either axis.
        /// </remarks>
        public int Origin { get; }
    }
}
