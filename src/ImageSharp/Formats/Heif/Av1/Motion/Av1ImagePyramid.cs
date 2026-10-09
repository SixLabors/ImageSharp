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
/// Every level is eight bits deep, whatever the coded depth. Every level has a replicated border, so the flow kernels can read past the edge of a
/// level without a boundary test.
/// </remarks>
internal sealed class Av1ImagePyramid : IDisposable
{
    /// <summary>
    /// The number of samples of replicated border on each side of every level.
    /// </summary>
    public const int Padding = 16;

    /// <summary>
    /// The byte multiple to which the row stride of every level is rounded up.
    /// </summary>
    private const int Alignment = 32;

    /// <summary>
    /// The base-two logarithm of the smallest level extent that the pyramid produces.
    /// </summary>
    private const int MinimumSizeLog2 = 3;

    /// <summary>
    /// The allocator of the level storage and of the halving buffers.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The geometry of every level that the frame size allows.
    /// </summary>
    private readonly Level[] levels;

    /// <summary>
    /// The owner of the storage of every level, or <see langword="null"/> after disposal.
    /// </summary>
    private IMemoryOwner<byte>? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ImagePyramid"/> class.
    /// </summary>
    /// <param name="allocator">The allocator of the level storage.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <remarks>
    /// The constructor reserves the storage for every level that the frame size allows in one allocation. The pyramid of a frame is built again for
    /// every reference that it is compared against, so one allocation keeps that cost low.
    /// </remarks>
    public Av1ImagePyramid(MemoryAllocator allocator, int width, int height)
    {
        this.allocator = allocator;
        this.LevelCount = GetMaximumLevelCount(width, height);
        this.levels = new Level[this.LevelCount];

        // Each level includes its border. The stride is aligned. Thus the first coded sample of every row has the alignment of the first coded
        // sample of the level.
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
    /// Halving stops when the shorter side reaches the smallest useful extent. Thus the count is the position of the most significant bit of the
    /// shorter side, less the logarithm of that extent.
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
    /// The span covers the border and the coded samples, because a span cannot express a negative index. A caller reads the sample at column x of
    /// row y as <c>Origin + (y * Stride) + x</c>. Both x and y can be negative, down to minus the padding.
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
    /// A pyramid is eight bits deep whatever the frame depth is. Thus the copy drops the low bits of each sample.
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

        // Every level below the first is halved from the level above it, which is already eight bits deep. Thus the rest of the work is the same
        // at each coded depth.
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
            // The first level is a copy of the frame, not a view of the frame buffer. Thus the pyramid owns every sample that it reads, and the
            // frame buffer does not get a second border layout. The samples are the same as in the frame.
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

            // The halving reads exactly twice the current extent. This clips the last row or column off a previous level of odd extent. The ratio
            // stays at exactly two, so a shift can rescale the flow field when it moves between levels.
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
    /// <param name="shift">The number of low bits to drop.</param>
    internal static void Narrow(ReadOnlySpan<ushort> source, Span<byte> destination, int shift)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = source.Length;
        int x = 0;

        // Two source vectors make one destination vector, because the narrow halves the width of a sample. The widest stage that the hardware
        // supports and that the row can fill runs first.
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
    /// The left and right borders repeat the end samples of each row. Then the top and bottom borders repeat the first and last full row. These
    /// full rows include the corners.
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
        /// The border fills the rows and columns before this index. Thus a caller can subtract up to the padding from it on each axis.
        /// </remarks>
        public int Origin { get; }
    }
}
