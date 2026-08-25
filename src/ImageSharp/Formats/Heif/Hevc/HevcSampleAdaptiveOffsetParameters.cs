// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies the HEVC sample-adaptive-offset classifier selected for one component coding-tree block.
/// </summary>
internal enum HevcSampleAdaptiveOffsetType : byte
{
    /// <summary>
    /// No sample-adaptive offset is applied.
    /// </summary>
    Off,

    /// <summary>
    /// Samples are classified by their most-significant sample-value band.
    /// </summary>
    Band,

    /// <summary>
    /// Samples are classified by horizontal neighboring samples.
    /// </summary>
    EdgeHorizontal,

    /// <summary>
    /// Samples are classified by vertical neighboring samples.
    /// </summary>
    EdgeVertical,

    /// <summary>
    /// Samples are classified by neighbors on the descending diagonal.
    /// </summary>
    EdgeDescending,

    /// <summary>
    /// Samples are classified by neighbors on the ascending diagonal.
    /// </summary>
    EdgeAscending,
}

/// <summary>
/// Contains the resolved HEVC sample-adaptive offsets for one component coding-tree block.
/// </summary>
internal readonly struct HevcSampleAdaptiveOffsetParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcSampleAdaptiveOffsetParameters"/> struct.
    /// </summary>
    /// <param name="type">The sample classifier.</param>
    /// <param name="bandPosition">The first of four consecutive band classes.</param>
    /// <param name="offset0">The first band or full-valley offset.</param>
    /// <param name="offset1">The second band or half-valley offset.</param>
    /// <param name="offset2">The third band or plain-edge offset.</param>
    /// <param name="offset3">The fourth band or half-peak offset.</param>
    /// <param name="offset4">The full-peak offset.</param>
    public HevcSampleAdaptiveOffsetParameters(
        HevcSampleAdaptiveOffsetType type,
        int bandPosition,
        int offset0,
        int offset1,
        int offset2,
        int offset3,
        int offset4)
    {
        this.Type = type;
        this.BandPosition = bandPosition;
        this.Offset0 = offset0;
        this.Offset1 = offset1;
        this.Offset2 = offset2;
        this.Offset3 = offset3;
        this.Offset4 = offset4;
    }

    /// <summary>
    /// Gets the sample classifier.
    /// </summary>
    public HevcSampleAdaptiveOffsetType Type { get; }

    /// <summary>
    /// Gets the first of four consecutive band classes.
    /// </summary>
    public int BandPosition { get; }

    /// <summary>
    /// Gets the first band or full-valley offset.
    /// </summary>
    public int Offset0 { get; }

    /// <summary>
    /// Gets the second band or half-valley offset.
    /// </summary>
    public int Offset1 { get; }

    /// <summary>
    /// Gets the third band or plain-edge offset.
    /// </summary>
    public int Offset2 { get; }

    /// <summary>
    /// Gets the fourth band or half-peak offset.
    /// </summary>
    public int Offset3 { get; }

    /// <summary>
    /// Gets the full-peak offset.
    /// </summary>
    public int Offset4 { get; }
}

/// <summary>
/// Identifies the slice and tile governing in-loop filtering for one coding-tree block.
/// </summary>
internal readonly struct HevcLoopFilterRegion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcLoopFilterRegion"/> struct.
    /// </summary>
    /// <param name="sliceStartAddressInTileScan">The first coding-tree block of the independent slice in tile-scan order.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="loopFilterAcrossSlicesEnabled">Whether the governing slice permits filtering across its slice boundary.</param>
    /// <param name="deblockingFilterDisabled">Whether the governing slice disables deblocking.</param>
    /// <param name="deblockingFilterBetaOffsetDiv2">Half the slice beta-threshold offset.</param>
    /// <param name="deblockingFilterTcOffsetDiv2">Half the slice clipping-threshold offset.</param>
    public HevcLoopFilterRegion(
        int sliceStartAddressInTileScan,
        int tileIndex,
        bool loopFilterAcrossSlicesEnabled,
        bool deblockingFilterDisabled,
        int deblockingFilterBetaOffsetDiv2,
        int deblockingFilterTcOffsetDiv2)
    {
        this.SliceStartAddressInTileScan = sliceStartAddressInTileScan;
        this.TileIndex = tileIndex;
        this.LoopFilterAcrossSlicesEnabled = loopFilterAcrossSlicesEnabled;
        this.DeblockingFilterDisabled = deblockingFilterDisabled;
        this.DeblockingFilterBetaOffsetDiv2 = deblockingFilterBetaOffsetDiv2;
        this.DeblockingFilterTcOffsetDiv2 = deblockingFilterTcOffsetDiv2;
    }

    /// <summary>
    /// Gets the first coding-tree block of the independent slice in tile-scan order.
    /// </summary>
    public int SliceStartAddressInTileScan { get; }

    /// <summary>
    /// Gets the zero-based tile index.
    /// </summary>
    public int TileIndex { get; }

    /// <summary>
    /// Gets a value indicating whether the governing slice permits filtering across its slice boundary.
    /// </summary>
    public bool LoopFilterAcrossSlicesEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether the governing slice disables deblocking.
    /// </summary>
    public bool DeblockingFilterDisabled { get; }

    /// <summary>
    /// Gets half the governing slice's beta-threshold offset.
    /// </summary>
    public int DeblockingFilterBetaOffsetDiv2 { get; }

    /// <summary>
    /// Gets half the governing slice's clipping-threshold offset.
    /// </summary>
    public int DeblockingFilterTcOffsetDiv2 { get; }
}

/// <summary>
/// Contains the eight coding-tree-block neighbor availability values used by HEVC in-loop filters.
/// </summary>
internal readonly struct HevcLoopFilterBoundaryAvailability
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcLoopFilterBoundaryAvailability"/> struct.
    /// </summary>
    /// <param name="left">Whether the left block is available.</param>
    /// <param name="right">Whether the right block is available.</param>
    /// <param name="above">Whether the block above is available.</param>
    /// <param name="below">Whether the block below is available.</param>
    /// <param name="aboveLeft">Whether the upper-left block is available.</param>
    /// <param name="aboveRight">Whether the upper-right block is available.</param>
    /// <param name="belowLeft">Whether the lower-left block is available.</param>
    /// <param name="belowRight">Whether the lower-right block is available.</param>
    public HevcLoopFilterBoundaryAvailability(
        bool left,
        bool right,
        bool above,
        bool below,
        bool aboveLeft,
        bool aboveRight,
        bool belowLeft,
        bool belowRight)
    {
        this.Left = left;
        this.Right = right;
        this.Above = above;
        this.Below = below;
        this.AboveLeft = aboveLeft;
        this.AboveRight = aboveRight;
        this.BelowLeft = belowLeft;
        this.BelowRight = belowRight;
    }

    /// <summary>
    /// Gets a value indicating whether the left block is available.
    /// </summary>
    public bool Left { get; }

    /// <summary>
    /// Gets a value indicating whether the right block is available.
    /// </summary>
    public bool Right { get; }

    /// <summary>
    /// Gets a value indicating whether the block above is available.
    /// </summary>
    public bool Above { get; }

    /// <summary>
    /// Gets a value indicating whether the block below is available.
    /// </summary>
    public bool Below { get; }

    /// <summary>
    /// Gets a value indicating whether the upper-left block is available.
    /// </summary>
    public bool AboveLeft { get; }

    /// <summary>
    /// Gets a value indicating whether the upper-right block is available.
    /// </summary>
    public bool AboveRight { get; }

    /// <summary>
    /// Gets a value indicating whether the lower-left block is available.
    /// </summary>
    public bool BelowLeft { get; }

    /// <summary>
    /// Gets a value indicating whether the lower-right block is available.
    /// </summary>
    public bool BelowRight { get; }
}

/// <summary>
/// Owns resolved sample-adaptive-offset parameters and prediction and filter region identifiers for one picture.
/// </summary>
internal sealed class HevcSampleAdaptiveOffsetState : IDisposable
{
    /// <summary>
    /// Whether any decoded component block enables sample-adaptive offset.
    /// </summary>
    private bool hasEnabledParameters;

    /// <summary>
    /// The three component records for every raster-ordered coding-tree block.
    /// </summary>
    private readonly IMemoryOwner<HevcSampleAdaptiveOffsetParameters> parameters;

    /// <summary>
    /// The independent-slice and tile prediction region of every coding-tree block.
    /// </summary>
    private readonly IMemoryOwner<int> regions;

    /// <summary>
    /// The independent-slice and tile filter region of every coding-tree block and color plane.
    /// </summary>
    private readonly IMemoryOwner<HevcLoopFilterRegion> loopFilterRegions;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcSampleAdaptiveOffsetState"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing pooled picture state.</param>
    /// <param name="codingTreeBlockCount">The raster-ordered coding-tree-block count.</param>
    public HevcSampleAdaptiveOffsetState(Configuration configuration, int codingTreeBlockCount)
    {
        this.parameters = configuration.MemoryAllocator.Allocate<HevcSampleAdaptiveOffsetParameters>(codingTreeBlockCount * 3);

        // Slice headers can disable SAO independently for luma and chroma. Initialize every component record to Off
        // so an enabled component never causes untouched records from pooled memory to enter the picture-level pass.
        this.parameters.Memory.Span.Clear();
        this.regions = configuration.MemoryAllocator.Allocate<int>(codingTreeBlockCount * 3);
        this.loopFilterRegions = configuration.MemoryAllocator.Allocate<HevcLoopFilterRegion>(codingTreeBlockCount * 3);
    }

    /// <summary>
    /// Gets a value indicating whether any component block enables sample-adaptive offset.
    /// </summary>
    public bool HasEnabledParameters => this.hasEnabledParameters;

    /// <summary>
    /// Gets the resolved component parameters for one coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The component plane.</param>
    /// <returns>The resolved sample-adaptive-offset parameters.</returns>
    public HevcSampleAdaptiveOffsetParameters Get(int rasterAddress, HevcPlane plane)
        => this.parameters.Memory.Span[(rasterAddress * 3) + (int)plane];

    /// <summary>
    /// Stores resolved component parameters for one coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="value">The resolved sample-adaptive-offset parameters.</param>
    public void Set(int rasterAddress, HevcPlane plane, HevcSampleAdaptiveOffsetParameters value)
    {
        this.parameters.Memory.Span[(rasterAddress * 3) + (int)plane] = value;
        this.hasEnabledParameters |= value.Type != HevcSampleAdaptiveOffsetType.Off;
    }

    /// <summary>
    /// Gets whether one coding-tree block belongs to the selected prediction region.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The independently coded color plane, or luma for combined-plane coding.</param>
    /// <param name="regionId">The current independent-slice and tile prediction-region identifier.</param>
    /// <returns><see langword="true"/> when the block belongs to the region; otherwise, <see langword="false"/>.</returns>
    public bool IsInRegion(int rasterAddress, HevcPlane plane, int regionId)
        => this.regions.Memory.Span[(rasterAddress * 3) + (int)plane] == regionId;

    /// <summary>
    /// Records the prediction region after one coding-tree block's parameters are decoded.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The independently coded color plane, or luma for combined-plane coding.</param>
    /// <param name="regionId">The positive prediction-region identifier.</param>
    public void SetRegion(int rasterAddress, HevcPlane plane, int regionId)
        => this.regions.Memory.Span[(rasterAddress * 3) + (int)plane] = regionId;

    /// <summary>
    /// Records the in-loop filter region for one coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The independently coded color plane, or luma for combined-plane coding.</param>
    /// <param name="value">The governing independent-slice and tile state.</param>
    public void SetLoopFilterRegion(int rasterAddress, HevcPlane plane, HevcLoopFilterRegion value)
        => this.loopFilterRegions.Memory.Span[(rasterAddress * 3) + (int)plane] = value;

    /// <summary>
    /// Derives the picture, slice, and tile boundary availability used by the in-loop filters for one coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The independently coded color plane, or luma for combined-plane coding.</param>
    /// <param name="pictureWidth">The picture width in coding-tree blocks.</param>
    /// <param name="pictureHeight">The picture height in coding-tree blocks.</param>
    /// <param name="loopFilterAcrossTilesEnabled">Whether the picture permits filtering across tile boundaries.</param>
    /// <returns>The availability of all eight neighboring coding-tree blocks.</returns>
    public HevcLoopFilterBoundaryAvailability GetLoopFilterBoundaryAvailability(
        int rasterAddress,
        HevcPlane plane,
        int pictureWidth,
        int pictureHeight,
        bool loopFilterAcrossTilesEnabled)
    {
        int x = rasterAddress % pictureWidth;
        int y = rasterAddress / pictureWidth;
        HevcLoopFilterRegion current = this.GetLoopFilterRegion(rasterAddress, plane);

        // H.265 assigns left, above, and upper-left boundaries to the current slice, while right, below, and
        // lower-right boundaries belong to the neighboring slice. This asymmetry makes filtering independent of CTB order.
        bool left = x > 0
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress - 1, plane), true, loopFilterAcrossTilesEnabled);
        bool right = x + 1 < pictureWidth
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress + 1, plane), false, loopFilterAcrossTilesEnabled);
        bool above = y > 0
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress - pictureWidth, plane), true, loopFilterAcrossTilesEnabled);
        bool below = y + 1 < pictureHeight
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress + pictureWidth, plane), false, loopFilterAcrossTilesEnabled);
        bool aboveLeft = x > 0 && y > 0
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress - pictureWidth - 1, plane), true, loopFilterAcrossTilesEnabled);
        bool belowRight = x + 1 < pictureWidth && y + 1 < pictureHeight
            && IsLoopFilterNeighborAvailable(current, this.GetLoopFilterRegion(rasterAddress + pictureWidth + 1, plane), false, loopFilterAcrossTilesEnabled);

        // The crossed diagonals do not have a fixed owner in raster order. The later independent slice owns the
        // boundary flag, which is identified by its greater tile-scan start address.
        bool aboveRight = x + 1 < pictureWidth && y > 0
            && IsLoopFilterDiagonalAvailable(current, this.GetLoopFilterRegion(rasterAddress - pictureWidth + 1, plane), loopFilterAcrossTilesEnabled);
        bool belowLeft = x > 0 && y + 1 < pictureHeight
            && IsLoopFilterDiagonalAvailable(current, this.GetLoopFilterRegion(rasterAddress + pictureWidth - 1, plane), loopFilterAcrossTilesEnabled);

        return new HevcLoopFilterBoundaryAvailability(left, right, above, below, aboveLeft, aboveRight, belowLeft, belowRight);
    }

    /// <summary>
    /// Gets the retained in-loop filter region for one coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <param name="plane">The independently coded color plane, or luma for combined-plane coding.</param>
    /// <returns>The retained slice and tile state.</returns>
    public HevcLoopFilterRegion GetLoopFilterRegion(int rasterAddress, HevcPlane plane)
        => this.loopFilterRegions.Memory.Span[(rasterAddress * 3) + (int)plane];

    /// <summary>
    /// Determines availability across a boundary with a direction-selected slice owner.
    /// </summary>
    /// <param name="current">The current block's region.</param>
    /// <param name="neighbor">The neighboring block's region.</param>
    /// <param name="currentOwnsSliceBoundary">Whether the current slice controls a boundary between different slices.</param>
    /// <param name="loopFilterAcrossTilesEnabled">Whether tile boundaries permit filtering.</param>
    /// <returns><see langword="true"/> when both slice and tile rules permit filtering.</returns>
    private static bool IsLoopFilterNeighborAvailable(
        HevcLoopFilterRegion current,
        HevcLoopFilterRegion neighbor,
        bool currentOwnsSliceBoundary,
        bool loopFilterAcrossTilesEnabled)
    {
        bool sameSlice = current.SliceStartAddressInTileScan == neighbor.SliceStartAddressInTileScan;
        bool sliceAvailable = sameSlice
            || (currentOwnsSliceBoundary ? current.LoopFilterAcrossSlicesEnabled : neighbor.LoopFilterAcrossSlicesEnabled);

        return sliceAvailable && (loopFilterAcrossTilesEnabled || current.TileIndex == neighbor.TileIndex);
    }

    /// <summary>
    /// Determines availability across a crossed-diagonal boundary using the later slice as its owner.
    /// </summary>
    /// <param name="current">The current block's region.</param>
    /// <param name="neighbor">The diagonally neighboring block's region.</param>
    /// <param name="loopFilterAcrossTilesEnabled">Whether tile boundaries permit filtering.</param>
    /// <returns><see langword="true"/> when both slice and tile rules permit filtering.</returns>
    private static bool IsLoopFilterDiagonalAvailable(
        HevcLoopFilterRegion current,
        HevcLoopFilterRegion neighbor,
        bool loopFilterAcrossTilesEnabled)
    {
        bool currentOwnsSliceBoundary = current.SliceStartAddressInTileScan > neighbor.SliceStartAddressInTileScan;
        return IsLoopFilterNeighborAvailable(current, neighbor, currentOwnsSliceBoundary, loopFilterAcrossTilesEnabled);
    }

    /// <summary>
    /// Releases the pooled sample-adaptive-offset picture state.
    /// </summary>
    public void Dispose()
    {
        this.loopFilterRegions.Dispose();
        this.regions.Dispose();
        this.parameters.Dispose();
    }
}
