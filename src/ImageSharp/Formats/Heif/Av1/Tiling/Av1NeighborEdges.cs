// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// The top and left edges of one neighbor array, read from its storage once by a caller and passed to the code that
/// reads and writes the contexts of many blocks.
/// </summary>
/// <typeparam name="T">The context value type.</typeparam>
internal readonly ref struct Av1NeighborEdges<T>
    where T : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborEdges{T}"/> struct.
    /// </summary>
    /// <param name="top">The contexts exposed to blocks below, one value per context unit along the width.</param>
    /// <param name="left">The contexts exposed to blocks on the right, one value per context unit along the height.</param>
    /// <param name="granularityLog2">The base-2 logarithm of the context unit size in samples.</param>
    public Av1NeighborEdges(Span<T> top, Span<T> left, int granularityLog2)
    {
        this.Top = top;
        this.Left = left;
        this.GranularityLog2 = granularityLog2;
    }

    /// <summary>
    /// Gets the contexts exposed to blocks below.
    /// </summary>
    public Span<T> Top { get; }

    /// <summary>
    /// Gets the contexts exposed to blocks on the right.
    /// </summary>
    public Span<T> Left { get; }

    /// <summary>
    /// Gets the base-2 logarithm of the context unit size in samples.
    /// </summary>
    public int GranularityLog2 { get; }

    /// <summary>
    /// Gets the top-edge unit index of a sample position.
    /// </summary>
    /// <param name="location">The sample position.</param>
    /// <returns>The index of the unit above the position.</returns>
    public int GetTopIndex(Point location) => location.X >> this.GranularityLog2;

    /// <summary>
    /// Gets the left-edge unit index of a sample position.
    /// </summary>
    /// <param name="location">The sample position.</param>
    /// <returns>The index of the unit left of the position.</returns>
    public int GetLeftIndex(Point location) => location.Y >> this.GranularityLog2;

    /// <summary>
    /// Writes one context value across the selected edges of a block: along its width on the top edge and along its
    /// height on the left edge.
    /// </summary>
    /// <param name="value">The context value to publish.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="blockSize">The block dimensions in samples.</param>
    /// <param name="mask">The edges to update.</param>
    public void Write(T value, Point origin, Size blockSize, Av1NeighborArrayUnit<T>.UnitMask mask)
    {
        // The bottom row of the block becomes the top edge of the blocks below it.
        if ((mask & Av1NeighborArrayUnit<T>.UnitMask.Top) != 0)
        {
            this.Top.Slice(this.GetTopIndex(origin), blockSize.Width >> this.GranularityLog2).Fill(value);
        }

        // The right column of the block becomes the left edge of the blocks on its right.
        if ((mask & Av1NeighborArrayUnit<T>.UnitMask.Left) != 0)
        {
            this.Left.Slice(this.GetLeftIndex(origin), blockSize.Height >> this.GranularityLog2).Fill(value);
        }
    }
}
