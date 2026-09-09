// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores left and top neighbor values at the granularity required by AV1 encoder contexts.
/// </summary>
/// <typeparam name="T">The context value type.</typeparam>
internal sealed class Av1NeighborArrayUnit<T> : IDisposable
    where T : struct
{
    /// <summary>
    /// Owns the contiguous neighbor storage until this instance is disposed.
    /// </summary>
    private IMemoryOwner<T>? owner;

    /// <summary>
    /// The contiguous neighbor storage, whether owned directly or supplied by a picture owner.
    /// </summary>
    private Memory<T> memory;

    /// <summary>
    /// Indicates whether the neighbor view has been disposed.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// The number of context values exposed to blocks on the right.
    /// </summary>
    private readonly int leftLength;

    /// <summary>
    /// The number of context values exposed to blocks below.
    /// </summary>
    private readonly int topLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborArrayUnit{T}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="leftSize">The number of values in the left-neighbor storage.</param>
    /// <param name="topSize">The number of values in the top-neighbor storage.</param>
    public Av1NeighborArrayUnit(Configuration configuration, int leftSize, int topSize)
    {
        this.leftLength = leftSize;
        this.topLength = topSize;
        int totalLength = checked(leftSize + topSize);

        // Both context edges share the picture lifetime, so one clean allocator-backed buffer
        // preserves their zero-initialized starting state without separate owner lifetimes.
        this.owner = configuration.MemoryAllocator.Allocate<T>(totalLength, AllocationOptions.Clean);
        this.memory = this.owner.Memory[..totalLength];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborArrayUnit{T}"/> class over non-owning picture-lifetime storage.
    /// </summary>
    /// <param name="memory">The contiguous left and top context storage.</param>
    /// <param name="leftSize">The number of values in the left-neighbor storage.</param>
    /// <param name="topSize">The number of values in the top-neighbor storage.</param>
    public Av1NeighborArrayUnit(Memory<T> memory, int leftSize, int topSize)
    {
        this.leftLength = leftSize;
        this.topLength = topSize;
        this.memory = memory[..checked(leftSize + topSize)];
    }

    /// <summary>
    /// Selects which neighbor arrays receive an update.
    /// </summary>
    [Flags]
    public enum UnitMask
    {
        /// <summary>
        /// Update the left-neighbor storage.
        /// </summary>
        Left = 1,

        /// <summary>
        /// Update the top-neighbor storage.
        /// </summary>
        Top = 2,
    }

    /// <summary>
    /// Gets the left-neighbor storage.
    /// </summary>
    public Span<T> Left
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);
            return this.memory.Span[..this.leftLength];
        }
    }

    /// <summary>
    /// Gets the top-neighbor storage.
    /// </summary>
    public Span<T> Top
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);
            return this.memory.Span.Slice(this.leftLength, this.topLength);
        }
    }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the top and left context granularity in samples.
    /// </summary>
    public required int GranularityNormalLog2 { get; set; }

    /// <summary>
    /// Gets the left-neighbor unit index for a sample position.
    /// </summary>
    /// <param name="loc">The sample position.</param>
    /// <returns>The left-neighbor unit index.</returns>
    public int GetLeftIndex(Point loc) => loc.Y >> this.GranularityNormalLog2;

    /// <summary>
    /// Gets the top-neighbor unit index for a sample position.
    /// </summary>
    /// <param name="loc">The sample position.</param>
    /// <returns>The top-neighbor unit index.</returns>
    public int GetTopIndex(Point loc) => loc.X >> this.GranularityNormalLog2;

    /// <summary>
    /// Returns the neighbor storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
        this.memory = Memory<T>.Empty;
        this.isDisposed = true;
    }

    /// <summary>
    /// Writes one context unit across the selected block edges.
    /// </summary>
    /// <param name="value">The context value to publish.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="blockSize">The block dimensions in samples.</param>
    /// <param name="mask">The neighbor arrays to update.</param>
    public void UnitModeWrite(T value, Point origin, Size blockSize, UnitMask mask)
    {
        if ((mask & UnitMask.Top) == UnitMask.Top)
        {
            // Top Neighbor Array
            //     ----------12345678---------------------
            //                ^    ^
            //                |    |
            //                |    |
            //               xxxxxxxx
            //               x      x
            //               x      x
            //               12345678
            //
            //  The top neighbor array is updated with the samples from the
            //    bottom row of the source block
            //
            //  Index = org_x
            int offset = this.GetTopIndex(origin);
            int count = blockSize.Width >> this.GranularityNormalLog2;

            // One packed value represents each AV1 edge unit. Filling the covered range mirrors the
            // contiguous above-context update without retaining a caller-owned span.
            this.Top.Slice(offset, count).Fill(value);
        }

        if ((mask & UnitMask.Left) == UnitMask.Left)
        {
            // Left Neighbor Array
            //
            //    |
            //    |
            //    1         xxxxxxx1
            //    2  <----  x      2
            //    3  <----  x      3
            //    4         xxxxxxx4
            //    |
            //    |
            //
            //  The left neighbor array is updated with the samples from the
            //    right column of the source block
            //
            //  Index = org_y
            int offset = this.GetLeftIndex(origin);
            int count = blockSize.Height >> this.GranularityNormalLog2;
            this.Left.Slice(offset, count).Fill(value);
        }
    }
}
