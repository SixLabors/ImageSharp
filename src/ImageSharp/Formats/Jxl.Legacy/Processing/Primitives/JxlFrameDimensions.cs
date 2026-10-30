// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Stores one parsed frame's pixel, block, and group dimensions.
/// </summary>
internal sealed class JxlFrameDimensions
{
    public const int BlockDimensions = 8;
    public const int DctBlockSize = BlockDimensions * BlockDimensions;
    public const int GroupDimensions = 256;
    public const int GroupDimensionsInBlocks = GroupDimensions / BlockDimensions;

    /// <summary>
    /// Derives pixel, block and group extents from a parsed frame header.
    /// </summary>
    /// <param name="xSizePixel">The frame width in pixels after upsampling.</param>
    /// <param name="ySizePixel">The frame height in pixels after upsampling.</param>
    /// <param name="groupSizeShift">The encoded group-size shift, from zero to three.</param>
    /// <param name="maxHorizontalShift">The largest horizontal chroma-subsampling shift.</param>
    /// <param name="maxVerticalShift">The largest vertical chroma-subsampling shift.</param>
    /// <param name="modularMode">Whether pixel extents exclude DCT padding.</param>
    /// <param name="upsampling">The encoded upsampling factor, which is a positive power of two up to eight.</param>
    public JxlFrameDimensions(
        int xSizePixel,
        int ySizePixel,
        int groupSizeShift,
        int maxHorizontalShift,
        int maxVerticalShift,
        bool modularMode,
        int upsampling)
    {
        this.GroupDimension = (GroupDimensions >> 1) << groupSizeShift;
        this.DcGroupDimension = this.GroupDimension * BlockDimensions;
        this.XSizeUpsampled = xSizePixel;
        this.YSizeUpsampled = ySizePixel;
        this.XSize = JxlMath.DivCeil(xSizePixel, upsampling);
        this.YSize = JxlMath.DivCeil(ySizePixel, upsampling);
        this.XSizeBlocks = JxlMath.DivCeil(this.XSize, BlockDimensions << maxHorizontalShift) << maxHorizontalShift;
        this.YSizeBlocks = JxlMath.DivCeil(this.YSize, BlockDimensions << maxVerticalShift) << maxVerticalShift;
        this.XSizePadded = this.XSizeBlocks * BlockDimensions;
        this.YSizePadded = this.YSizeBlocks * BlockDimensions;

        if (modularMode)
        {
            this.XSizePadded = this.XSize;
            this.YSizePadded = this.YSize;
        }

        this.XSizeUpsampledPadded = this.XSizePadded * upsampling;
        this.YSizeUpsampledPadded = this.YSizePadded * upsampling;

        // GroupSizeShift selects 128, 256, 512 or 1024 pixels per group side. The fixed 256-pixel default is not the parsed group size.
        // DC groups span that same number of blocks per side, so their count divides the block extent rather than the pixel extent.
        this.XSizeGroups = JxlMath.DivCeil(this.XSize, this.GroupDimension);
        this.YSizeGroups = JxlMath.DivCeil(this.YSize, this.GroupDimension);
        this.XSizeDcGroups = JxlMath.DivCeil(this.XSizeBlocks, this.GroupDimension);
        this.YSizeDcGroups = JxlMath.DivCeil(this.YSizeBlocks, this.GroupDimension);
        this.NumGroups = checked(this.XSizeGroups * this.YSizeGroups);
        this.NumDcGroups = checked(this.XSizeDcGroups * this.YSizeDcGroups);
    }

    public int XSize { get; set; }

    public int YSize { get; set; }

    public int XSizeUpsampled { get; set; }

    public int YSizeUpsampled { get; set; }

    public int XSizeUpsampledPadded { get; set; }

    public int YSizeUpsampledPadded { get; set; }

    public int XSizePadded { get; set; }

    public int YSizePadded { get; set; }

    public int XSizeBlocks { get; set; }

    public int YSizeBlocks { get; set; }

    public int XSizeGroups { get; set; }

    public int YSizeGroups { get; set; }

    public int XSizeDcGroups { get; set; }

    public int YSizeDcGroups { get; set; }

    public int NumGroups { get; set; }

    public int NumDcGroups { get; set; }

    public int GroupDimension { get; set; }

    public int DcGroupDimension { get; set; }

    /// <summary>
    /// Gets an AC group's rectangle in logical pixels before frame upsampling.
    /// </summary>
    /// <param name="groupIndex">The validated row-major AC group index, less than <see cref="NumGroups"/>.</param>
    /// <returns>The pixel rectangle clipped to the logical frame, excluding DCT padding.</returns>
    public Rectangle GroupRect(int groupIndex)
    {
        (int groupY, int groupX) = Math.DivRem(groupIndex, this.XSizeGroups);
        int x = groupX * this.GroupDimension;
        int y = groupY * this.GroupDimension;

        return new(x, y, Math.Min(this.GroupDimension, this.XSize - x), Math.Min(this.GroupDimension, this.YSize - y));
    }

    /// <summary>
    /// Gets an AC group's rectangle in the padded grid of blocks of 8 by 8 pixels.
    /// </summary>
    /// <param name="groupIndex">The validated row-major AC group index, less than <see cref="NumGroups"/>.</param>
    /// <returns>The block rectangle clipped to the frame's padded block extent.</returns>
    public Rectangle BlockGroupRect(int groupIndex)
    {
        (int groupY, int groupX) = Math.DivRem(groupIndex, this.XSizeGroups);
        int blocksPerGroup = this.GroupDimension / BlockDimensions;
        int x = groupX * blocksPerGroup;
        int y = groupY * blocksPerGroup;

        return new(x, y, Math.Min(blocksPerGroup, this.XSizeBlocks - x), Math.Min(blocksPerGroup, this.YSizeBlocks - y));
    }

    /// <summary>
    /// Gets a DC group's rectangle in the padded grid of blocks of 8 by 8 pixels.
    /// </summary>
    /// <param name="groupIndex">The validated row-major DC group index, less than <see cref="NumDcGroups"/>.</param>
    /// <returns>The block rectangle clipped to the frame's padded block extent.</returns>
    public Rectangle DcGroupRect(int groupIndex)
    {
        // A DC group spans GroupDimension blocks per side, eight times the AC group's block extent.
        // DcGroupDimension expresses that span in pixels and must not be used with these block coordinates.
        (int groupY, int groupX) = Math.DivRem(groupIndex, this.XSizeDcGroups);
        int x = groupX * this.GroupDimension;
        int y = groupY * this.GroupDimension;

        return new(x, y, Math.Min(this.GroupDimension, this.XSizeBlocks - x), Math.Min(this.GroupDimension, this.YSizeBlocks - y));
    }
}
