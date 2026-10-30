// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Stores the JPEG XL preview dimensions and their compact wire representation.
/// </summary>
internal sealed class JxlPreviewHeader : IJxlFields
{
    /// <summary>
    /// Compact dimensions use units of eight pixels. The four alternatives cover 1 through 544 units.
    /// </summary>
    private static readonly JxlU32Enc DividedDimensionEncoding = new(
        JxlFieldExpressions.Value(16),
        JxlFieldExpressions.Value(32),
        JxlFieldExpressions.BitsOffset(5, 1),
        JxlFieldExpressions.BitsOffset(9, 33));

    /// <summary>
    /// Explicit pixel dimensions occupy consecutive ranges: 1..64, 65..320, 321..1344, and 1345..5440.
    /// </summary>
    private static readonly JxlU32Enc DimensionEncoding = new(
        JxlFieldExpressions.BitsOffset(6, 1),
        JxlFieldExpressions.BitsOffset(8, 65),
        JxlFieldExpressions.BitsOffset(10, 321),
        JxlFieldExpressions.BitsOffset(12, 1345));

    private bool div8;
    private int ySizeDiv8;
    private int ySize;
    private int ratio;
    private int xSizeDiv8;
    private int xSize;

    /// <summary>
    /// Initializes a preview header with the wire defaults of one pixel in each dimension.
    /// </summary>
    public JxlPreviewHeader() => JxlBundle.Init(this);

    /// <summary>
    /// Gets the preview height in pixels.
    /// </summary>
    public int YSize => this.div8 ? (this.ySizeDiv8 * 8) : this.ySize;

    /// <summary>
    /// Gets the preview width in pixels. A built-in aspect ratio truncates the derived width to a whole pixel.
    /// </summary>
    public int XSize
    {
        get
        {
            if (this.ratio != 0)
            {
                SignedRational signedRational = JxlAspectRatioHelpers.FixedAspectRatios(this.ratio);

                return (int)JxlAspectRatioHelpers.MultiplyTruncate(signedRational, this.YSize);
            }

            return this.div8 ? (this.xSizeDiv8 * 8) : this.xSize;
        }
    }

    /// <summary>
    /// Selects the native compact representation of the supplied positive preview dimensions.
    /// </summary>
    /// <param name="x">The preview width in pixels.</param>
    /// <param name="y">The preview height in pixels.</param>
    public void Set(int x, int y)
    {
        if (x <= 0 || y <= 0)
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

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.Boolean(false, ref this.div8))
        {
            return false;
        }

        // Preview dimensions have their own smaller distributions. Do not use the full image-size distribution here.
        // Separate conditions also let the default visitor initialize both stored representations.
        if (visitor.Conditional(this.div8) && !visitor.U32(DividedDimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.ySizeDiv8)))
        {
            return false;
        }

        if (visitor.Conditional(!this.div8) && !visitor.U32(DimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.ySize)))
        {
            return false;
        }

        if (!visitor.Bits(3, 0, ref Unsafe.As<int, uint>(ref this.ratio)))
        {
            return false;
        }

        // A nonzero ratio supplies the width. Otherwise the width uses the same distribution and units as the height.
        if (visitor.Conditional(this.ratio == 0 && this.div8) && !visitor.U32(DividedDimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.xSizeDiv8)))
        {
            return false;
        }

        if (visitor.Conditional(this.ratio == 0 && !this.div8) && !visitor.U32(DimensionEncoding, 1, ref Unsafe.As<int, uint>(ref this.xSize)))
        {
            return false;
        }

        // Wire dimensions are at most 5440 pixels, so even the widest built-in ratio fits the Int32 dimension contract.
        return true;
    }
}
