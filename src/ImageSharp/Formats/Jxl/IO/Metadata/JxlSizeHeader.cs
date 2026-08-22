// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlSizeHeader : IJxlFields
{
    private bool isSmall;
    private int ySizeDiv8Minus1;
    private int ySize;
    private int ratio;
    private int xSizeDiv8Minus1;
    private int xSize;

    public int YSize => this.isSmall ? ((this.ySizeDiv8Minus1 + 1) * 8) : this.ySize;

    public int XSize
    {
        get
        {
            if (this.ratio != 0)
            {
                SignedRational aspectRatio = JxlAspectRatioHelpers.FixedAspectRatios(this.ratio);

                return JxlAspectRatioHelpers.MultiplyTruncate(aspectRatio, this.YSize);
            }

            return this.isSmall ? ((this.xSizeDiv8Minus1 + 1) * 8) : this.xSize;
        }
    }

    public void Set(int x, int y)
    {
        if (x > int.MaxValue || y > int.MaxValue)
        {
            throw new ArgumentException("Image too large");
        }

        if (x == 0 || y == 0)
        {
            throw new ArgumentException("Empty image");
        }

        this.ratio = JxlAspectRatioHelpers.FindAspectRatio(x, y);
        this.isSmall = y < 256 && (y % JxlFrameDimensions.BlockDimensions) == 0
                    && (this.ratio != 0 || (x <= 256 && (x % JxlFrameDimensions.BlockDimensions) == 0));

        if (this.isSmall)
        {
            this.ySizeDiv8Minus1 = (y / 8) - 1;
        }
        else
        {
            this.ySize = y;
        }

        if (this.ratio == 0)
        {
            if (this.isSmall)
            {
                this.xSizeDiv8Minus1 = (x / 8) - 1;
            }
            else
            {
                this.xSize = x;
            }
        }
    }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
