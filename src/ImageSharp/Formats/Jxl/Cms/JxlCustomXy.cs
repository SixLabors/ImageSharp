// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

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

    public int X { get; set; }

    public int Y { get; set; }

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
