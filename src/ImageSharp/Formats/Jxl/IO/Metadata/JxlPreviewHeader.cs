// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlPreviewHeader : IJxlFields
{
    private bool div8;
    private int ySizeDiv8;
    private int ySize;
    private int ratio;
    private int xSizeDiv8;
    private int xSize;

    public int YSize => this.div8 ? (this.ySizeDiv8 * 8) : this.ySize;

    public int XSize
    {
        get
        {
            if (this.ratio != 0)
            {
                SignedRational signedRational = JxlAspectRatioHelpers.FixedAspectRatios(this.ratio);

                return JxlAspectRatioHelpers.MultiplyTruncate(signedRational, this.YSize);
            }

            return this.div8 ? (this.xSizeDiv8 * 8) : this.xSize;
        }
    }

    public void Set(int x, int y)
    {
        if (x == 0 || y == 0)
        {
            throw new ArgumentException("Empty preview");
        }

        this.div8 = ((x % JxlFrameDimensions.BlockDimensions) | (y % JxlFrameDimensions.BlockDimensions)) == 0;

        if (this.div8)
        {
            this.ySizeDiv8 = y / 8;
        }
        else
        {
            this.ySize = y;
        }

        this.ratio = JxlAspectRatioHelpers.FindAspectRatio(x, y);

        if (this.ratio == 0)
        {
            if (this.div8)
            {
                this.xSizeDiv8 = x / 8;
            }
            else
            {
                this.xSize = x;
            }
        }
    }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
