// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal sealed class JxlFrameDimensions
{
    public const int BlockDimensions = 8;
    public const int DctBlockSize = BlockDimensions * BlockDimensions;
    public const int GroupDimensions = 256;
    public const int GroupDimensionsInBlocks = GroupDimensions / BlockDimensions;

    public JxlFrameDimensions(int xSizePixel, int ySizePixel, int groupSizeShift, int maxHorizontalShift, int maxVerticalShift, bool modularMode, int upsampling)
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
        this.XSizeGroups = JxlMath.DivCeil(this.XSize, GroupDimensions);
        this.YSizeGroups = JxlMath.DivCeil(this.YSize, GroupDimensions);
        this.XSizeDcGroups = JxlMath.DivCeil(this.XSizeBlocks, GroupDimensions);
        this.YSizeDcGroups = JxlMath.DivCeil(this.YSizeBlocks, GroupDimensions);
        this.NumGroups = this.XSizeGroups * this.YSizeGroups;
        this.NumDcGroups = this.XSizeDcGroups * this.YSizeDcGroups;
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
}
