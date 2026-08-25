// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Tracks reconstructed minimum prediction blocks for HEVC intra-reference availability.
/// </summary>
internal sealed class HevcReconstructionState : IDisposable
{
    /// <summary>
    /// The base-two logarithm of the minimum luma prediction-block side.
    /// </summary>
    private const int MinPredictionBlockLog2 = 2;

    /// <summary>
    /// The reconstruction-region identifiers for the three component planes.
    /// </summary>
    private readonly Buffer2D<int>[] regions;

    /// <summary>
    /// The horizontal chroma subsampling shift.
    /// </summary>
    private readonly int chromaSubsamplingX;

    /// <summary>
    /// The vertical chroma subsampling shift.
    /// </summary>
    private readonly int chromaSubsamplingY;

    /// <summary>
    /// The coded luma width used to reject padded right-edge units.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// The coded luma height used to reject padded bottom-edge units.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcReconstructionState"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="sequenceParameterSet">The coded picture and chroma geometry.</param>
    public HevcReconstructionState(Configuration configuration, HevcSequenceParameterSet sequenceParameterSet)
    {
        this.width = sequenceParameterSet.Width;
        this.height = sequenceParameterSet.Height;
        int widthInUnits = DivideCeilingByPowerOfTwo(this.width, MinPredictionBlockLog2);
        int heightInUnits = DivideCeilingByPowerOfTwo(this.height, MinPredictionBlockLog2);
        this.chromaSubsamplingX = !sequenceParameterSet.SeparateColorPlaneFlag && sequenceParameterSet.ChromaFormat is 1 or 2 ? 1 : 0;
        this.chromaSubsamplingY = !sequenceParameterSet.SeparateColorPlaneFlag && sequenceParameterSet.ChromaFormat == 1 ? 1 : 0;

        // Region identifiers gate every reconstructed-neighbor read. A stale pooled identifier can match the first
        // region of a later picture, so these maps must begin at the reserved unavailable value zero.
        this.regions =
        [
            configuration.MemoryAllocator.Allocate2D<int>(widthInUnits, heightInUnits, AllocationOptions.Clean),
            configuration.MemoryAllocator.Allocate2D<int>(widthInUnits, heightInUnits, AllocationOptions.Clean),
            configuration.MemoryAllocator.Allocate2D<int>(widthInUnits, heightInUnits, AllocationOptions.Clean),
        ];
    }

    /// <summary>
    /// Gets the horizontal availability-unit width for a component plane.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <returns>The availability-unit width in component samples.</returns>
    public int GetUnitWidth(HevcPlane plane) => 1 << (MinPredictionBlockLog2 - this.GetSubsamplingX(plane));

    /// <summary>
    /// Gets the vertical availability-unit height for a component plane.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <returns>The availability-unit height in component samples.</returns>
    public int GetUnitHeight(HevcPlane plane) => 1 << (MinPredictionBlockLog2 - this.GetSubsamplingY(plane));

    /// <summary>
    /// Marks a reconstructed component rectangle as available within one slice-and-tile prediction region.
    /// </summary>
    /// <param name="plane">The reconstructed component plane.</param>
    /// <param name="x">The rectangle left coordinate in component samples.</param>
    /// <param name="y">The rectangle top coordinate in component samples.</param>
    /// <param name="width">The rectangle width in component samples.</param>
    /// <param name="height">The rectangle height in component samples.</param>
    /// <param name="regionId">The positive identifier shared by prediction blocks in the same slice segment and tile.</param>
    public void MarkReconstructed(HevcPlane plane, int x, int y, int width, int height, int regionId)
    {
        DebugGuard.MustBeGreaterThan(regionId, 0, nameof(regionId));
        int subsamplingX = this.GetSubsamplingX(plane);
        int subsamplingY = this.GetSubsamplingY(plane);
        int unitX = (x << subsamplingX) >> MinPredictionBlockLog2;
        int unitY = (y << subsamplingY) >> MinPredictionBlockLog2;
        int endX = DivideCeilingByPowerOfTwo((x + width) << subsamplingX, MinPredictionBlockLog2);
        int endY = DivideCeilingByPowerOfTwo((y + height) << subsamplingY, MinPredictionBlockLog2);
        Buffer2D<int> map = this.regions[(int)plane];
        endX = Math.Min(endX, map.Width);
        endY = Math.Min(endY, map.Height);

        // Chroma availability units map back to the same four-by-four luma grid used by HEVC neighbor derivation.
        // Filling the complete rectangle makes later sub-TUs observe only samples whose reconstruction has finished.
        for (int row = unitY; row < endY; row++)
        {
            map.DangerousGetRowSpan(row)[unitX..endX].Fill(regionId);
        }
    }

    /// <summary>
    /// Builds the ordered availability flags consumed by HEVC reference-sample substitution.
    /// </summary>
    /// <param name="plane">The component plane containing the prediction block.</param>
    /// <param name="x">The prediction-block left coordinate in component samples.</param>
    /// <param name="y">The prediction-block top coordinate in component samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="regionId">The current slice-and-tile prediction-region identifier.</param>
    /// <param name="destination">
    /// The destination ordered from the bottom-most below-left unit through top-left and then the above-right units.
    /// </param>
    /// <returns>The number of flags written.</returns>
    public int BuildReferenceAvailability(HevcPlane plane, int x, int y, int log2Size, int regionId, Span<bool> destination)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        DebugGuard.MustBeGreaterThan(regionId, 0, nameof(regionId));
        int size = 1 << log2Size;
        int unitWidth = this.GetUnitWidth(plane);
        int unitHeight = this.GetUnitHeight(plane);
        int leftUnitCount = (size * 2) / unitHeight;
        int aboveUnitCount = (size * 2) / unitWidth;
        int flagCount = leftUnitCount + aboveUnitCount + 1;
        Span<bool> availability = destination[..flagCount];

        for (int unit = 0; unit < leftUnitCount; unit++)
        {
            int unitY = y + ((leftUnitCount - unit - 1) * unitHeight);
            availability[unit] = this.IsAvailable(plane, x - 1, unitY, regionId);
        }

        availability[leftUnitCount] = this.IsAvailable(plane, x - 1, y - 1, regionId);
        for (int unit = 0; unit < aboveUnitCount; unit++)
        {
            availability[leftUnitCount + unit + 1] = this.IsAvailable(plane, x + (unit * unitWidth), y - 1, regionId);
        }

        return flagCount;
    }

    /// <summary>
    /// Gets whether one component sample has already been reconstructed in the selected prediction region.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The component sample X coordinate.</param>
    /// <param name="y">The component sample Y coordinate.</param>
    /// <param name="regionId">The current slice-and-tile prediction-region identifier.</param>
    /// <returns><see langword="true"/> when the sample is available; otherwise, <see langword="false"/>.</returns>
    public bool IsReconstructed(HevcPlane plane, int x, int y, int regionId) => this.IsAvailable(plane, x, y, regionId);

    /// <summary>
    /// Releases the owned reconstruction-region maps.
    /// </summary>
    public void Dispose()
    {
        foreach (Buffer2D<int> map in this.regions)
        {
            map.Dispose();
        }
    }

    /// <summary>
    /// Gets whether a component sample belongs to an already reconstructed block in the selected prediction region.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The component sample X coordinate.</param>
    /// <param name="y">The component sample Y coordinate.</param>
    /// <param name="regionId">The current slice-and-tile prediction-region identifier.</param>
    /// <returns><see langword="true"/> when the sample is available; otherwise, <see langword="false"/>.</returns>
    private bool IsAvailable(HevcPlane plane, int x, int y, int regionId)
    {
        if (x < 0 || y < 0)
        {
            return false;
        }

        int subsamplingX = this.GetSubsamplingX(plane);
        int subsamplingY = this.GetSubsamplingY(plane);
        int planeWidth = DivideCeilingByPowerOfTwo(this.width, subsamplingX);
        int planeHeight = DivideCeilingByPowerOfTwo(this.height, subsamplingY);
        if (x >= planeWidth || y >= planeHeight)
        {
            return false;
        }

        int unitX = (x << subsamplingX) >> MinPredictionBlockLog2;
        int unitY = (y << subsamplingY) >> MinPredictionBlockLog2;
        Buffer2D<int> map = this.regions[(int)plane];
        return (uint)unitX < (uint)map.Width
            && (uint)unitY < (uint)map.Height
            && map.DangerousGetRowSpan(unitY)[unitX] == regionId;
    }

    /// <summary>
    /// Gets the horizontal chroma shift selected by a component plane.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <returns>Zero for luma and full-resolution planes; otherwise, the chroma shift.</returns>
    private int GetSubsamplingX(HevcPlane plane) => plane == HevcPlane.Y ? 0 : this.chromaSubsamplingX;

    /// <summary>
    /// Gets the vertical chroma shift selected by a component plane.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <returns>Zero for luma and full-resolution planes; otherwise, the chroma shift.</returns>
    private int GetSubsamplingY(HevcPlane plane) => plane == HevcPlane.Y ? 0 : this.chromaSubsamplingY;

    /// <summary>
    /// Divides a nonnegative sample count by a power of two with upward rounding.
    /// </summary>
    /// <param name="value">The sample count.</param>
    /// <param name="shift">The base-two divisor logarithm.</param>
    /// <returns>The upward-rounded quotient.</returns>
    private static int DivideCeilingByPowerOfTwo(int value, int shift) => (value + (1 << shift) - 1) >> shift;
}
