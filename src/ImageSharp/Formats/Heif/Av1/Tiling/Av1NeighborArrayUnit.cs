// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores left, top, and top-left neighbor values at the granularity required by AV1 encoder contexts.
/// </summary>
/// <typeparam name="T">The context value type, including its invalid sentinel value.</typeparam>
internal sealed class Av1NeighborArrayUnit<T> : IDisposable
    where T : struct, IMinMaxValue<T>
{
    /// <summary>
    /// The sentinel used for neighbor positions that have not been populated.
    /// </summary>
    public static readonly T InvalidNeighborData = T.MaxValue;

    /// <summary>
    /// Owns the contiguous neighbor storage until this instance is disposed.
    /// </summary>
    private IMemoryOwner<T>? memory;

    /// <summary>
    /// The number of context values exposed to blocks on the right.
    /// </summary>
    private readonly int leftLength;

    /// <summary>
    /// The number of context values exposed to blocks below.
    /// </summary>
    private readonly int topLength;

    /// <summary>
    /// The number of context values indexed by the diagonal difference between horizontal and vertical positions.
    /// </summary>
    private readonly int topLeftLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborArrayUnit{T}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="leftSize">The number of values in the left-neighbor storage.</param>
    /// <param name="topSize">The number of values in the top-neighbor storage.</param>
    /// <param name="topLeftSize">The number of values in the diagonal-neighbor storage.</param>
    public Av1NeighborArrayUnit(Configuration configuration, int leftSize, int topSize, int topLeftSize)
    {
        this.leftLength = leftSize;
        this.topLength = topSize;
        this.topLeftLength = topLeftSize;
        int totalLength = checked(leftSize + topSize + topLeftSize);

        // All three neighbor regions share the picture lifetime, so one clean allocator-backed
        // buffer avoids three managed arrays and preserves their zero-initialized starting state.
        this.memory = configuration.MemoryAllocator.Allocate<T>(totalLength, AllocationOptions.Clean);
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

        /// <summary>
        /// Update the top-left diagonal storage.
        /// </summary>
        TopLeft = 4,
    }

    /// <summary>
    /// Gets the left-neighbor storage.
    /// </summary>
    public Span<T> Left
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.memory is null, this);
            return this.memory.Memory.Span[..this.leftLength];
        }
    }

    /// <summary>
    /// Gets the top-neighbor storage.
    /// </summary>
    public Span<T> Top
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.memory is null, this);
            return this.memory.Memory.Span.Slice(this.leftLength, this.topLength);
        }
    }

    /// <summary>
    /// Gets the top-left diagonal storage.
    /// </summary>
    public Span<T> TopLeft
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.memory is null, this);
            return this.memory.Memory.Span.Slice(this.leftLength + this.topLength, this.topLeftLength);
        }
    }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the top and left context granularity in samples.
    /// </summary>
    public required int GranularityNormalLog2 { get; set; }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the diagonal context granularity in samples.
    /// </summary>
    public required int GranularityTopLeftLog2 { get; set; }

    /// <summary>
    /// Gets the number of consecutive values stored for each neighbor-array unit.
    /// </summary>
    public int UnitSize { get; private set; }

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
    /// Gets the diagonal-neighbor unit index for a sample position.
    /// </summary>
    /// <param name="loc">The sample position.</param>
    /// <returns>The top-left neighbor index derived from the position's diagonal.</returns>
    public int GetTopLeftIndex(Point loc)
        => this.leftLength + (loc.X >> this.GranularityTopLeftLog2) - (loc.Y >> this.GranularityTopLeftLog2);

    /// <summary>
    /// Returns the neighbor storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.memory?.Dispose();
        this.memory = null;
    }

    /// <summary>
    /// Writes one context unit across the selected block edges.
    /// </summary>
    /// <param name="value">The values that make up one context unit.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="blockSize">The block dimensions in samples.</param>
    /// <param name="mask">The neighbor arrays to update.</param>
    public void UnitModeWrite(ReadOnlySpan<T> value, Point origin, Size blockSize, UnitMask mask)
    {
        int idx, j;

        int count;
        int na_offset;
        int na_unit_size;

        na_unit_size = this.UnitSize;

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
            na_offset = this.GetTopIndex(origin);

            ref T dst_ptr = ref this.Top[na_offset * na_unit_size];

            count = blockSize.Width >> this.GranularityNormalLog2;

            for (idx = 0; idx < count; ++idx)
            {
                // Unit sizes are deliberately tiny, so direct ref copies avoid slicing for every neighbor position.
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
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
            na_offset = this.GetLeftIndex(origin);

            ref T dst_ptr = ref this.Left[na_offset * na_unit_size];

            count = blockSize.Height >> this.GranularityNormalLog2;

            for (idx = 0; idx < count; ++idx)
            {
                // Unit sizes are deliberately tiny, so direct ref copies avoid slicing for every neighbor position.
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
        }

        if ((mask & UnitMask.TopLeft) == UnitMask.TopLeft)
        {
            // Top-left Neighbor Array
            //
            //    4-5--6--7------------
            //    3 \      \
            //    2  \      \
            //    1   \      \
            //    |\   xxxxxx7
            //    | \  x     6
            //    |  \ x     5
            //    |   \1x2x3x4
            //    |
            //
            //  The top-left neighbor array is updated with the reversed samples
            //    from the right column and bottom row of the source block
            //
            // Index = org_x - org_y
            Point topLeft = origin;
            topLeft.Offset(0, blockSize.Height - 1);
            na_offset = this.GetTopLeftIndex(topLeft);

            // Copy bottom-row + right-column
            // *Note - start from the bottom-left corner
            ref T dst_ptr = ref this.TopLeft[na_offset * na_unit_size];

            count = ((blockSize.Width + blockSize.Height) >> this.GranularityTopLeftLog2) - 1;

            for (idx = 0; idx < count; ++idx)
            {
                // Unit sizes are deliberately tiny, so direct ref copies avoid slicing for every neighbor position.
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
        }
    }
}
