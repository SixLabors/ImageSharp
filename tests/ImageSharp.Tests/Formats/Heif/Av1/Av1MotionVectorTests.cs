// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 motion-vector precision, range, spatial-clamp, and temporal-projection semantics.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionVectorTests
{
    /// <summary>
    /// Checks frame-relative displacement limits and their inclusive last candidate.
    /// </summary>
    /// <param name="x">The block column.</param>
    /// <param name="y">The block row.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="border">The allocated luma border.</param>
    /// <param name="minimumColumn">The first legal horizontal displacement.</param>
    /// <param name="minimumRow">The first legal vertical displacement.</param>
    /// <param name="maximumColumn">The last legal horizontal displacement.</param>
    /// <param name="maximumRow">The last legal vertical displacement.</param>
    [Theory]
    [InlineData(0, 0, 8, 8, 96, -16, -16, 264, 264)]
    [InlineData(128, 64, 8, 8, 96, -144, -80, 136, 200)]
    [InlineData(252, 252, 8, 8, 96, -268, -268, 12, 12)]
    [InlineData(0, 0, 128, 128, 96, -88, -88, 216, 216)]
    [InlineData(0, 0, 128, 128, 160, -136, -136, 264, 264)]
    public void FrameSearchBoundsIncludePositionBlockExtentAndInterpolation(
        int x,
        int y,
        int width,
        int height,
        int border,
        int minimumColumn,
        int minimumRow,
        int maximumColumn,
        int maximumRow)
    {
        Rectangle bounds = Av1MotionVector.GetFrameSearchBounds(new Rectangle(x, y, width, height), new Size(256, 256), border);
        Assert.Equal(Rectangle.FromLTRB(minimumColumn, minimumRow, maximumColumn + 1, maximumRow + 1), bounds);
        Assert.True(bounds.Contains(minimumColumn, minimumRow));
        Assert.True(bounds.Contains(maximumColumn, maximumRow));
        Assert.False(bounds.Contains(maximumColumn + 1, maximumRow));
        Assert.False(bounds.Contains(maximumColumn, maximumRow + 1));
    }

    /// <summary>
    /// Checks inward rounding around fractional references and exclusion of reserved vector endpoints.
    /// </summary>
    /// <param name="row">The reference row in eighth-sample units.</param>
    /// <param name="column">The reference column in eighth-sample units.</param>
    /// <param name="fullMinimumColumn">The first full-pixel column.</param>
    /// <param name="fullMinimumRow">The first full-pixel row.</param>
    /// <param name="fullMaximumColumn">The last full-pixel column.</param>
    /// <param name="fullMaximumRow">The last full-pixel row.</param>
    /// <param name="fractionalMinimumColumn">The first eighth-sample column.</param>
    /// <param name="fractionalMinimumRow">The first eighth-sample row.</param>
    /// <param name="fractionalMaximumColumn">The last eighth-sample column.</param>
    /// <param name="fractionalMaximumRow">The last eighth-sample row.</param>
    [Theory]
    [InlineData(1, -1, -1023, -1022, 1022, 1023, -8185, -8183, 8183, 8185)]
    [InlineData(-1, 1, -1022, -1023, 1023, 1022, -8183, -8185, 8185, 8183)]
    [InlineData(16376, -16376, -2047, 1024, -1024, 2047, -16383, 8192, -8192, 16383)]
    [InlineData(-16376, 16376, 1024, -2047, 2047, -1024, 8192, -16383, 16383, -8192)]
    public void SearchBoundsRoundInwardAndExcludeReservedVectorEndpoints(
        int row,
        int column,
        int fullMinimumColumn,
        int fullMinimumRow,
        int fullMaximumColumn,
        int fullMaximumRow,
        int fractionalMinimumColumn,
        int fractionalMinimumRow,
        int fractionalMaximumColumn,
        int fractionalMaximumRow)
    {
        Av1MotionVector reference = new(row, column);
        Rectangle frameBounds = Rectangle.FromLTRB(-4000, -4000, 4001, 4001);
        Rectangle full = reference.GetFullPixelSearchBounds(frameBounds);
        Rectangle fractional = reference.GetSubpixelSearchBounds(frameBounds);
        Assert.Equal(Rectangle.FromLTRB(fullMinimumColumn, fullMinimumRow, fullMaximumColumn + 1, fullMaximumRow + 1), full);
        Assert.Equal(Rectangle.FromLTRB(fractionalMinimumColumn, fractionalMinimumRow, fractionalMaximumColumn + 1, fractionalMaximumRow + 1), fractional);
        Assert.True(fractional.Contains(full.Left * 8, full.Top * 8));
        Assert.True(fractional.Contains((full.Right - 1) * 8, (full.Bottom - 1) * 8));
    }

    /// <summary>
    /// Verifies that reference-centered limits cannot widen a tighter padded-frame region.
    /// </summary>
    [Fact]
    public void FullAndFractionalSearchRetainTighterFrameBounds()
    {
        Rectangle frameBounds = Rectangle.FromLTRB(-17, -29, 32, 44);
        Av1MotionVector reference = new(1, -1);
        Assert.Equal(frameBounds, reference.GetFullPixelSearchBounds(frameBounds));
        Assert.Equal(Rectangle.FromLTRB(-136, -232, 249, 345), reference.GetSubpixelSearchBounds(frameBounds));
    }

    /// <summary>
    /// Verifies that high-precision vectors retain their one-eighth-sample components unchanged.
    /// </summary>
    [Fact]
    public void LowerPrecisionRetainsHighPrecisionComponents()
    {
        Av1MotionVector vector = new(15, -15);

        Assert.Equal(vector, vector.LowerPrecision(allowHighPrecision: true, forceInteger: false));
    }

    /// <summary>
    /// Verifies that low precision removes odd one-eighth-sample components toward zero.
    /// </summary>
    /// <param name="row">The original vertical component.</param>
    /// <param name="column">The original horizontal component.</param>
    /// <param name="expectedRow">The expected low-precision vertical component.</param>
    /// <param name="expectedColumn">The expected low-precision horizontal component.</param>
    [Theory]
    [InlineData(15, -15, 14, -14)]
    [InlineData(14, -14, 14, -14)]
    [InlineData(1, -1, 0, 0)]
    public void LowerPrecisionReducesOddComponentsTowardZero(int row, int column, int expectedRow, int expectedColumn)
    {
        Av1MotionVector actual = new Av1MotionVector(row, column).LowerPrecision(allowHighPrecision: false, forceInteger: false);

        Assert.Equal(new Av1MotionVector(expectedRow, expectedColumn), actual);
    }

    /// <summary>
    /// Verifies AV1 integer-sample rounding, including half-sample ties toward zero on both signs.
    /// </summary>
    /// <param name="component">The original component in one-eighth-sample units.</param>
    /// <param name="expected">The expected integer-precision component.</param>
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 8)]
    [InlineData(11, 8)]
    [InlineData(12, 8)]
    [InlineData(13, 16)]
    [InlineData(16, 16)]
    [InlineData(-3, 0)]
    [InlineData(-4, 0)]
    [InlineData(-5, -8)]
    [InlineData(-11, -8)]
    [InlineData(-12, -8)]
    [InlineData(-13, -16)]
    [InlineData(-16, -16)]
    public void LowerPrecisionRoundsIntegerHalfTiesTowardZero(int component, int expected)
    {
        Av1MotionVector actual = new Av1MotionVector(component, -component).LowerPrecision(
            allowHighPrecision: true,
            forceInteger: true);

        Assert.Equal(new Av1MotionVector(expected, -expected), actual);
    }

    /// <summary>
    /// Verifies that AV1 reserves both signed motion-vector endpoints.
    /// </summary>
    /// <param name="row">The vertical component.</param>
    /// <param name="column">The horizontal component.</param>
    /// <param name="expected">The expected validity.</param>
    [Theory]
    [InlineData(-16383, 16383, true)]
    [InlineData(-16384, 0, false)]
    [InlineData(-16385, 0, false)]
    [InlineData(16384, 0, false)]
    [InlineData(16385, 0, false)]
    [InlineData(0, -16384, false)]
    [InlineData(0, 16384, false)]
    public void IsValidUsesExclusiveMotionVectorEndpoints(int row, int column, bool expected)
        => Assert.Equal(expected, new Av1MotionVector(row, column).IsValid);

    /// <summary>
    /// Verifies the complete-block and sixteen-sample borders used to clamp spatial reference candidates.
    /// </summary>
    [Fact]
    public void ClampReferenceMatchesSpatialLimits()
    {
        const int blockWidth = 16;
        const int blockHeight = 8;
        const int blockToLeftEdge = -256;
        const int blockToRightEdge = 512;
        const int blockToTopEdge = -128;
        const int blockToBottomEdge = 384;

        Av1MotionVector upper = new Av1MotionVector(1000, 1000).ClampReference(
            blockWidth,
            blockHeight,
            blockToLeftEdge,
            blockToRightEdge,
            blockToTopEdge,
            blockToBottomEdge);

        Av1MotionVector lower = new Av1MotionVector(-1000, -1000).ClampReference(
            blockWidth,
            blockHeight,
            blockToLeftEdge,
            blockToRightEdge,
            blockToTopEdge,
            blockToBottomEdge);

        Av1MotionVector inside = new Av1MotionVector(48, -64).ClampReference(
            blockWidth,
            blockHeight,
            blockToLeftEdge,
            blockToRightEdge,
            blockToTopEdge,
            blockToBottomEdge);

        Assert.Equal(new Av1MotionVector(576, 768), upper);
        Assert.Equal(new Av1MotionVector(-320, -512), lower);
        Assert.Equal(new Av1MotionVector(48, -64), inside);
    }

    /// <summary>
    /// Verifies AV1 fixed-point temporal projection, distance limiting, symmetric rounding, and endpoint clamping.
    /// </summary>
    /// <param name="row">The source vertical component.</param>
    /// <param name="column">The source horizontal component.</param>
    /// <param name="numerator">The signed source-to-target frame distance.</param>
    /// <param name="denominator">The positive source-to-reference frame distance.</param>
    /// <param name="expectedRow">The expected projected vertical component.</param>
    /// <param name="expectedColumn">The expected projected horizontal component.</param>
    [Theory]
    [InlineData(64, -96, 2, 4, 32, -48)]
    [InlineData(64, -96, -2, 4, -32, 48)]
    [InlineData(2, -2, 1, 3, 1, -1)]
    [InlineData(31, -31, 40, 40, 31, -31)]
    [InlineData(4095, -4095, 31, 1, 16383, -16383)]
    public void ProjectTemporalMatchesReference(
        int row,
        int column,
        int numerator,
        int denominator,
        int expectedRow,
        int expectedColumn)
    {
        Av1MotionVector actual = new Av1MotionVector(row, column).ProjectTemporal(numerator, denominator);

        Assert.Equal(new Av1MotionVector(expectedRow, expectedColumn), actual);
    }
}
