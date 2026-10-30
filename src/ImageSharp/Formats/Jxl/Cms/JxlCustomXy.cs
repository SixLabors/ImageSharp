// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// A serializable form of CieXyChromaticityCoordinates
/// </summary>
internal struct JxlCustomXy
{
    private const uint Multiplier = 1000000;
    private const float RoughLimit = 4.0f;
    private const int Min = -0x200000;
    private const int Max = 0x1FFFFF;

    /// <summary>
    /// Wire alternatives for both sign-packed coordinates. Offsets begin at the corresponding power of two.
    /// </summary>
    private static readonly JxlU32Enc CoordinateEncoding = new(
        JxlFieldExpressions.Bits(19),
        JxlFieldExpressions.BitsOffset(19, 1U << 19),
        JxlFieldExpressions.BitsOffset(20, 1U << 20),
        JxlFieldExpressions.BitsOffset(21, 1U << 21));

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// Visits the signed integer coordinates without boxing this value type.
    /// </summary>
    /// <param name="visitor">The header field operation.</param>
    /// <returns>Whether both coordinates could be processed.</returns>
    public bool Visit(JxlVisitor visitor)
    {
        // The U32 alternatives encode a signed coordinate after sign packing. Their maximum packed value
        // is 4194303, which restores the complete coordinate range from -2097152 through 2097151.
        uint x = JxlPackSigned.PackUnsigned(this.X);

        if (!visitor.U32(CoordinateEncoding, 0, ref x))
        {
            return false;
        }

        this.X = JxlPackSigned.UnpackSigned(x);
        uint y = JxlPackSigned.PackUnsigned(this.Y);

        if (!visitor.U32(CoordinateEncoding, 0, ref y))
        {
            return false;
        }

        this.Y = JxlPackSigned.UnpackSigned(y);
        return true;
    }

    public readonly CieXyChromaticityCoordinates GetValue() => new(
        x: this.X * (1.0f / Multiplier),
        y: this.Y * (1.0f / Multiplier));

    public bool SetValue(CieXyChromaticityCoordinates xy)
    {
        bool ok = (Math.Abs(xy.X) < RoughLimit) && (Math.Abs(xy.Y) < RoughLimit);

        if (!ok)
        {
            throw new InvalidOperationException("X or Y is out of bounds");
        }

        this.X = (int)MathF.Round((float)(xy.X * Multiplier));

        if (this.X is < Min or > Max)
        {
            throw new InvalidOperationException("X is out of bounds");
        }

        this.Y = (int)MathF.Round((float)(xy.Y * Multiplier));

        if (this.Y is < Min or > Max)
        {
            throw new InvalidOperationException("Y is out of bounds");
        }

        return true;
    }

    public readonly bool IsSame(CieXyChromaticityCoordinates other) => this.X == other.X && this.Y == other.Y;
}
