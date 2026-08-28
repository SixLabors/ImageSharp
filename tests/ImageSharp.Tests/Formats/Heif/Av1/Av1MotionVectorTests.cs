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
    public void ClampReferenceMatchesLibaomSpatialLimits()
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
    public void ProjectTemporalMatchesLibaom(
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
