// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Stores the JPEG XL image dimensions and their compact wire representation.
/// </summary>
internal sealed class JxlSizeHeader : IJxlFields
{
    /// <summary>
    /// Dimension alternatives store the positive dimension minus one in 9, 13, 18, or 30 bits.
    /// </summary>
    private static readonly JxlU32Enc DimensionEncoding = new(
        JxlFieldExpressions.BitsOffset(9, 1),
        JxlFieldExpressions.BitsOffset(13, 1),
        JxlFieldExpressions.BitsOffset(18, 1),
        JxlFieldExpressions.BitsOffset(30, 1));

    private bool isSmall;
    private int ySizeDiv8Minus1;
    private int ySize;
    private int ratio;
    private int xSizeDiv8Minus1;
    private int xSize;

    /// <summary>
    /// Initializes a size header with the wire defaults of one pixel in each dimension.
    /// </summary>
    public JxlSizeHeader() => JxlBundle.Init(this);

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int YSize => this.isSmall ? ((this.ySizeDiv8Minus1 + 1) * 8) : this.ySize;

    /// <summary>
    /// Gets the image width in pixels. A built-in aspect ratio truncates the derived width to a whole pixel.
    /// </summary>
    public int XSize
    {
        get
        {
            if (this.ratio != 0)
            {
                SignedRational aspectRatio = JxlAspectRatioHelpers.FixedAspectRatios(this.ratio);

                return (int)JxlAspectRatioHelpers.MultiplyTruncate(aspectRatio, this.YSize);
            }

            return this.isSmall ? ((this.xSizeDiv8Minus1 + 1) * 8) : this.xSize;
        }
    }

    /// <summary>
    /// Selects the compact representation of the supplied positive image dimensions.
    /// </summary>
    /// <param name="x">The image width in pixels.</param>
    /// <param name="y">The image height in pixels.</param>
    public void Set(int x, int y)
    {
        if (x <= 0 || y <= 0)
        {
            throw new ArgumentException("Image dimensions must be positive.");
        }

        this.ratio = JxlAspectRatioHelpers.FindAspectRatio(x, y);
        this.isSmall = y <= 256 && (y % JxlFrameDimensions.BlockDimensions) == 0
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

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.Boolean(false, ref this.isSmall))
        {
            return false;
        }

        // The small form stores multiples of eight from 8 through 256. Other dimensions use the four U32 alternatives.
        // Keep the two conditions separate: the default visitor must initialize both stored representations.
        if (visitor.Conditional(this.isSmall) && !visitor.Bits(5, 0, ref Unsafe.As<int, uint>(ref this.ySizeDiv8Minus1)))
        {
            return false;
        }

        if (visitor.Conditional(!this.isSmall) && !visitor.U32(DimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.ySize)))
        {
            return false;
        }

        // Ratio zero sends the width explicitly. Codes 1 through 7 derive it from the height, with integer truncation.
        if (!visitor.Bits(3, 0, ref Unsafe.As<int, uint>(ref this.ratio)))
        {
            return false;
        }

        if (visitor.Conditional(this.ratio == 0 && this.isSmall) && !visitor.Bits(5, 0, ref Unsafe.As<int, uint>(ref this.xSizeDiv8Minus1)))
        {
            return false;
        }

        if (visitor.Conditional(this.ratio == 0 && !this.isSmall) && !visitor.U32(DimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.xSize)))
        {
            return false;
        }

        // The encoded height fits Int32. A derived width can be twice that height, so validate it before the getter narrows it.
        return this.ratio == 0 || JxlAspectRatioHelpers.MultiplyTruncate(JxlAspectRatioHelpers.FixedAspectRatios(this.ratio), this.YSize) <= int.MaxValue;
    }
}
