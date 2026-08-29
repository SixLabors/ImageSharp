// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Tracks luma transform and prediction boundaries at the four-sample resolution used to derive HEVC deblocking edges.
/// </summary>
internal sealed class HevcDeblockingState : IDisposable
{
    /// <summary>
    /// The base-two logarithm of the boundary-map unit side.
    /// </summary>
    private const int UnitLog2 = 2;

    /// <summary>
    /// The packed flag identifying a vertical boundary at a unit's left edge.
    /// </summary>
    private const byte VerticalBoundary = 1 << 0;

    /// <summary>
    /// The packed flag identifying a horizontal boundary at a unit's top edge.
    /// </summary>
    private const byte HorizontalBoundary = 1 << 1;

    /// <summary>
    /// The boundary maps for the primary plane of combined coding or each independently coded color plane.
    /// </summary>
    private readonly Buffer2D<byte>[] boundaries;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcDeblockingState"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="sequenceParameterSet">The coded picture dimensions.</param>
    public HevcDeblockingState(Configuration configuration, HevcSequenceParameterSet sequenceParameterSet)
    {
        int width = DivideCeilingByPowerOfTwo(sequenceParameterSet.Width, UnitLog2);
        int height = DivideCeilingByPowerOfTwo(sequenceParameterSet.Height, UnitLog2);

        Buffer2D<byte>? lumaBoundaries = null;
        Buffer2D<byte>? chromaBlueBoundaries = null;
        Buffer2D<byte>? chromaRedBoundaries = null;
        try
        {
            // MarkBlock combines sparse edge flags with existing values, so zero initialization is part of the state contract.
            lumaBoundaries = configuration.MemoryAllocator.Allocate2D<byte>(width, height, AllocationOptions.Clean);
            chromaBlueBoundaries = configuration.MemoryAllocator.Allocate2D<byte>(width, height, AllocationOptions.Clean);
            chromaRedBoundaries = configuration.MemoryAllocator.Allocate2D<byte>(width, height, AllocationOptions.Clean);
            this.boundaries = [lumaBoundaries, chromaBlueBoundaries, chromaRedBoundaries];
        }
        catch
        {
            chromaRedBoundaries?.Dispose();
            chromaBlueBoundaries?.Dispose();
            lumaBoundaries?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Records the left and top edges of one leaf transform or pulse-code-modulated coding block.
    /// </summary>
    /// <param name="plane">The primary coding plane.</param>
    /// <param name="x">The block left coordinate in full-resolution primary-plane samples.</param>
    /// <param name="y">The block top coordinate in full-resolution primary-plane samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public void MarkBlock(HevcPlane plane, int x, int y, int width, int height)
    {
        Buffer2D<byte> map = this.boundaries[(int)plane];
        int unitX = x >> UnitLog2;
        int unitY = y >> UnitLog2;
        int endX = Math.Min(DivideCeilingByPowerOfTwo(x + width, UnitLog2), map.Width);
        int endY = Math.Min(DivideCeilingByPowerOfTwo(y + height, UnitLog2), map.Height);

        // A transform boundary covers every four-sample segment along its edge. Packing both orientations into one
        // byte keeps the decoder state contiguous and lets the later eight-sample deblocking traversal reject edges cheaply.
        for (int row = unitY; row < endY; row++)
        {
            map.DangerousGetRowSpan(row)[unitX] |= VerticalBoundary;
        }

        Span<byte> top = map.DangerousGetRowSpan(unitY);
        for (int column = unitX; column < endX; column++)
        {
            top[column] |= HorizontalBoundary;
        }
    }

    /// <summary>
    /// Gets whether a four-sample segment begins at a vertical transform or prediction boundary.
    /// </summary>
    /// <param name="plane">The primary coding plane.</param>
    /// <param name="x">The segment left coordinate in full-resolution primary-plane samples.</param>
    /// <param name="y">The segment top coordinate in full-resolution primary-plane samples.</param>
    /// <returns><see langword="true"/> when the segment is a vertical boundary; otherwise, <see langword="false"/>.</returns>
    public bool IsVerticalBoundary(HevcPlane plane, int x, int y)
        => (this.boundaries[(int)plane].DangerousGetRowSpan(y >> UnitLog2)[x >> UnitLog2] & VerticalBoundary) != 0;

    /// <summary>
    /// Gets whether a four-sample segment begins at a horizontal transform or prediction boundary.
    /// </summary>
    /// <param name="plane">The primary coding plane.</param>
    /// <param name="x">The segment left coordinate in full-resolution primary-plane samples.</param>
    /// <param name="y">The segment top coordinate in full-resolution primary-plane samples.</param>
    /// <returns><see langword="true"/> when the segment is a horizontal boundary; otherwise, <see langword="false"/>.</returns>
    public bool IsHorizontalBoundary(HevcPlane plane, int x, int y)
        => (this.boundaries[(int)plane].DangerousGetRowSpan(y >> UnitLog2)[x >> UnitLog2] & HorizontalBoundary) != 0;

    /// <summary>
    /// Releases the allocator-owned boundary maps.
    /// </summary>
    public void Dispose()
    {
        foreach (Buffer2D<byte> map in this.boundaries)
        {
            map.Dispose();
        }
    }

    /// <summary>
    /// Divides a nonnegative sample count by a power of two with upward rounding.
    /// </summary>
    /// <param name="value">The sample count.</param>
    /// <param name="shift">The base-two divisor logarithm.</param>
    /// <returns>The upward-rounded quotient.</returns>
    private static int DivideCeilingByPowerOfTwo(int value, int shift) => (value + (1 << shift) - 1) >> shift;
}
