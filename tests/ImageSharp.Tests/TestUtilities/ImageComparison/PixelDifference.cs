// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

public readonly struct PixelDifference
{
    public PixelDifference(
        Point position,
        int redDifference,
        int greenDifference,
        int blueDifference,
        int alphaDifference)
    {
        this.Position = position;
        this.RedDifference = redDifference;
        this.GreenDifference = greenDifference;
        this.BlueDifference = blueDifference;
        this.AlphaDifference = alphaDifference;
    }

    public PixelDifference(Point position, Rgba64 expected, Rgba64 actual)
        : this(
            position,
            actual.R - expected.R,
            actual.G - expected.G,
            actual.B - expected.B,
            actual.A - expected.A)
    {
    }

    public Point Position { get; }

    public int RedDifference { get; }

    public int GreenDifference { get; }

    public int BlueDifference { get; }

    public int AlphaDifference { get; }

    public override string ToString() =>
        $"[Δ({this.RedDifference},{this.GreenDifference},{this.BlueDifference},{this.AlphaDifference}) @ ({this.Position.X},{this.Position.Y})]";
}
