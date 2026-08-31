// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 global-motion-vector derivation against the fixed-point rules used by the reference decoder.
/// </summary>
[Trait("Format", "Avif")]
public class Av1GlobalMotionParametersTests
{
    /// <summary>
    /// Verifies that an identity model produces no displacement at every block position.
    /// </summary>
    [Fact]
    public void IdentityModelProducesZeroMotionVector()
    {
        Av1GlobalMotionParameters parameters = Av1GlobalMotionParameters.Identity;

        Av1MotionVector actual = parameters.GetMotionVector(
            allowHighPrecisionMotionVector: true,
            Av1BlockSize.Block128x128,
            new Point(31, 17),
            forceIntegerMotionVector: false);

        Assert.Equal(default, actual);
    }

    /// <summary>
    /// Verifies the published AV1 translation-component ordering and optional integer precision reduction.
    /// </summary>
    [Theory]
    [InlineData(false, 19, -21)]
    [InlineData(true, 16, -24)]
    public void TranslationModelMatchesNormativeComponentOrdering(bool forceIntegerMotionVector, int expectedRow, int expectedColumn)
    {
        Av1GlobalMotionParameters parameters = Av1GlobalMotionParameters.Identity;
        parameters.Type = Av1GlobalMotionType.Translation;

        // Translation parameters retain sixteen fractional bits; the derived vector retains three.
        parameters[0] = 19 << 13;
        parameters[1] = -21 << 13;

        Av1MotionVector actual = parameters.GetMotionVector(
            allowHighPrecisionMotionVector: true,
            Av1BlockSize.Block16x16,
            new Point(4, 7),
            forceIntegerMotionVector);

        Assert.Equal(new Av1MotionVector(expectedRow, expectedColumn), actual);
    }

    /// <summary>
    /// Verifies affine evaluation at the AV1 block center for high- and low-precision vector output.
    /// </summary>
    [Theory]
    [InlineData(true, 1, 3)]
    [InlineData(false, 0, 2)]
    public void AffineModelEvaluatesBlockCenter(bool allowHighPrecisionMotionVector, int expectedRow, int expectedColumn)
    {
        Av1GlobalMotionParameters parameters = Av1GlobalMotionParameters.Identity;
        parameters.Type = Av1GlobalMotionType.Affine;

        // The 8x8 block at mode-info position (2, 3) has center (11, 15). These deltas produce horizontal and
        // vertical fixed-point offsets that exercise signed rounding at the selected output precision.
        parameters[0] = 2048;
        parameters[1] = -1024;
        parameters[2] = Av1GlobalMotionParameters.ModelScale + 1024;
        parameters[3] = 512;
        parameters[4] = -256;
        parameters[5] = Av1GlobalMotionParameters.ModelScale + 768;

        Av1MotionVector actual = parameters.GetMotionVector(
            allowHighPrecisionMotionVector,
            Av1BlockSize.Block8x8,
            new Point(2, 3),
            forceIntegerMotionVector: false);

        Assert.Equal(new Av1MotionVector(expectedRow, expectedColumn), actual);
    }

    /// <summary>
    /// Verifies local least-squares projection against a multi-sample model traced from the reference decoder.
    /// </summary>
    [Fact]
    public void LocalProjectionMatchesReference()
    {
        Point[] sourcePoints = [new(24, -40), new(-40, 24), new(-24, -24), new(72, -24)];
        Point[] referencePoints = [new(-16, -8), new(-72, 64), new(-64, 16), new(32, 8)];

        Av1GlobalMotionParameters parameters = Av1GlobalMotionParameters.DeriveLocalProjection(
            sourcePoints,
            referencePoints,
            Av1BlockSize.Block8x8,
            new Av1MotionVector(32, -40),
            new Point(8, 6));

        Assert.Equal(Av1GlobalMotionType.Affine, parameters.Type);
        Assert.False(parameters.IsInvalid);
        Assert.Equal(-191565, parameters[0]);
        Assert.Equal(599107, parameters[1]);
        Assert.Equal(61755, parameters[2]);
        Assert.Equal(-140, parameters[3]);
        Assert.Equal(-6909, parameters[4]);
        Assert.Equal(62012, parameters[5]);
        Assert.Equal(-3776, parameters.Alpha);
        Assert.Equal(-128, parameters.Beta);
        Assert.Equal(-7360, parameters.Gamma);
        Assert.Equal(-3520, parameters.Delta);
    }
}
