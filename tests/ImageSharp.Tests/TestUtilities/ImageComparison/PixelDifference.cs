// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

public readonly struct PixelDifference
{
    /// <summary>
    /// Initializes a difference in integer component units.
    /// </summary>
    /// <param name="position">The pixel position.</param>
    /// <param name="redDifference">The red component difference.</param>
    /// <param name="greenDifference">The green component difference.</param>
    /// <param name="blueDifference">The blue component difference.</param>
    /// <param name="alphaDifference">The alpha component difference.</param>
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

    /// <summary>
    /// Initializes a difference in unsigned sixteen-bit component units.
    /// </summary>
    /// <param name="position">The pixel position.</param>
    /// <param name="expected">The expected pixel.</param>
    /// <param name="actual">The actual pixel.</param>
    public PixelDifference(Point position, Rgba64 expected, Rgba64 actual)
        : this(
            position,
            actual.R - expected.R,
            actual.G - expected.G,
            actual.B - expected.B,
            actual.A - expected.A)
    {
    }

    /// <summary>
    /// Initializes a floating-point difference expressed in sixteen-bit component units without quantization.
    /// </summary>
    /// <param name="position">The pixel position.</param>
    /// <param name="expected">The expected components.</param>
    /// <param name="actual">The actual components.</param>
    public PixelDifference(Point position, RgbaVector expected, RgbaVector actual)
    {
        this.Position = position;

        // Match the integer reports' units while preserving fractional codes and out-of-range values.
        this.RedDifference = (actual.R - expected.R) * ushort.MaxValue;
        this.GreenDifference = (actual.G - expected.G) * ushort.MaxValue;
        this.BlueDifference = (actual.B - expected.B) * ushort.MaxValue;
        this.AlphaDifference = (actual.A - expected.A) * ushort.MaxValue;
    }

    /// <summary>
    /// Gets the pixel position.
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Gets the red difference in the component units supplied to the constructor.
    /// </summary>
    public float RedDifference { get; }

    /// <summary>
    /// Gets the green difference in the component units supplied to the constructor.
    /// </summary>
    public float GreenDifference { get; }

    /// <summary>
    /// Gets the blue difference in the component units supplied to the constructor.
    /// </summary>
    public float BlueDifference { get; }

    /// <summary>
    /// Gets the alpha difference in the component units supplied to the constructor.
    /// </summary>
    public float AlphaDifference { get; }

    /// <inheritdoc />
    public override string ToString() =>
        $"[Δ({this.RedDifference},{this.GreenDifference},{this.BlueDifference},{this.AlphaDifference}) @ ({this.Position.X},{this.Position.Y})]";
}
