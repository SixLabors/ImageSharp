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
    /// Verifies search bounds, precision reduction, validity, spatial clamping, and temporal projection against
    /// independently derived reference values.
    /// </summary>
    [Fact]
    public void MotionVectorOperationsMatchReference()
    {
        // Frame-relative displacement limits include the block extent, the allocated border, and the interpolation
        // margin; the maximum candidate is inclusive.
        AssertFrameSearchBounds(new Rectangle(0, 0, 8, 8), 96, Rectangle.FromLTRB(-16, -16, 265, 265));
        AssertFrameSearchBounds(new Rectangle(252, 252, 8, 8), 96, Rectangle.FromLTRB(-268, -268, 13, 13));
        AssertFrameSearchBounds(new Rectangle(0, 0, 128, 128), 160, Rectangle.FromLTRB(-136, -136, 265, 265));

        // Reference-centered limits round inward around fractional references, exclude the reserved endpoints,
        // and never widen a tighter padded-frame region.
        Rectangle wideFrame = Rectangle.FromLTRB(-4000, -4000, 4001, 4001);
        Av1MotionVector fractionalReference = new(1, -1);
        Assert.Equal(Rectangle.FromLTRB(-1023, -1022, 1023, 1024), fractionalReference.GetFullPixelSearchBounds(wideFrame));
        Assert.Equal(Rectangle.FromLTRB(-8185, -8183, 8184, 8186), fractionalReference.GetSubpixelSearchBounds(wideFrame));
        Av1MotionVector extremeReference = new(16376, -16376);
        Assert.Equal(Rectangle.FromLTRB(-2047, 1024, -1023, 2048), extremeReference.GetFullPixelSearchBounds(wideFrame));
        Assert.Equal(Rectangle.FromLTRB(-16383, 8192, -8191, 16384), extremeReference.GetSubpixelSearchBounds(wideFrame));
        Rectangle tightFrame = Rectangle.FromLTRB(-17, -29, 32, 44);
        Assert.Equal(tightFrame, fractionalReference.GetFullPixelSearchBounds(tightFrame));
        Assert.Equal(Rectangle.FromLTRB(-136, -232, 249, 345), fractionalReference.GetSubpixelSearchBounds(tightFrame));

        // High precision keeps one-eighth-sample components, low precision drops odd components toward zero, and
        // integer precision rounds half-sample ties toward zero on both signs.
        Av1MotionVector odd = new(15, -15);
        Assert.Equal(odd, odd.LowerPrecision(allowHighPrecision: true, forceInteger: false));
        Assert.Equal(new Av1MotionVector(14, -14), odd.LowerPrecision(allowHighPrecision: false, forceInteger: false));
        Assert.Equal(new Av1MotionVector(0, 0), new Av1MotionVector(1, -1).LowerPrecision(allowHighPrecision: false, forceInteger: false));
        Assert.Equal(new Av1MotionVector(0, 0), new Av1MotionVector(4, -4).LowerPrecision(allowHighPrecision: true, forceInteger: true));
        Assert.Equal(new Av1MotionVector(8, -8), new Av1MotionVector(5, -5).LowerPrecision(allowHighPrecision: true, forceInteger: true));
        Assert.Equal(new Av1MotionVector(8, -8), new Av1MotionVector(12, -12).LowerPrecision(allowHighPrecision: true, forceInteger: true));
        Assert.Equal(new Av1MotionVector(-8, 8), new Av1MotionVector(-5, 5).LowerPrecision(allowHighPrecision: true, forceInteger: true));
        Assert.Equal(new Av1MotionVector(-8, 8), new Av1MotionVector(-12, 12).LowerPrecision(allowHighPrecision: true, forceInteger: true));

        // Both signed endpoints are reserved.
        Assert.True(new Av1MotionVector(-16383, 16383).IsValid);
        Assert.False(new Av1MotionVector(-16384, 0).IsValid);
        Assert.False(new Av1MotionVector(16384, 0).IsValid);
        Assert.False(new Av1MotionVector(0, -16384).IsValid);
        Assert.False(new Av1MotionVector(0, 16384).IsValid);

        // Spatial candidates clamp to the complete block plus a sixteen-sample border.
        Assert.Equal(new Av1MotionVector(576, 768), new Av1MotionVector(1000, 1000).ClampReference(16, 8, -256, 512, -128, 384));
        Assert.Equal(new Av1MotionVector(-320, -512), new Av1MotionVector(-1000, -1000).ClampReference(16, 8, -256, 512, -128, 384));
        Assert.Equal(new Av1MotionVector(48, -64), new Av1MotionVector(48, -64).ClampReference(16, 8, -256, 512, -128, 384));

        // Temporal projection uses fixed-point distance scaling, symmetric rounding, distance limiting, and clamping.
        Assert.Equal(new Av1MotionVector(32, -48), new Av1MotionVector(64, -96).ProjectTemporal(2, 4));
        Assert.Equal(new Av1MotionVector(-32, 48), new Av1MotionVector(64, -96).ProjectTemporal(-2, 4));
        Assert.Equal(new Av1MotionVector(1, -1), new Av1MotionVector(2, -2).ProjectTemporal(1, 3));
        Assert.Equal(new Av1MotionVector(31, -31), new Av1MotionVector(31, -31).ProjectTemporal(40, 40));
        Assert.Equal(new Av1MotionVector(16383, -16383), new Av1MotionVector(4095, -4095).ProjectTemporal(31, 1));

        static void AssertFrameSearchBounds(Rectangle block, int border, Rectangle expected)
            => Assert.Equal(expected, Av1MotionVector.GetFrameSearchBounds(block, new Size(256, 256), border));
    }
}
