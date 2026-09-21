// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests grayscale ICC conversion against the XYZ and Lab achromatic axes.
/// </summary>
[Trait("Color", "Conversion")]
public class GrayTrcCalculatorTests
{
    /// <summary>
    /// Verifies that a gamma curve produces neutral PCS black, midtones, and white.
    /// </summary>
    /// <param name="pcsType">The profile connection space.</param>
    /// <param name="gray">The device gray value.</param>
    /// <param name="connection">The expected normalized achromatic PCS value.</param>
    [Theory]
    [InlineData(IccColorSpaceType.CieXyz, 0F, 0F)]
    [InlineData(IccColorSpaceType.CieXyz, 0.5F, 0.25F)]
    [InlineData(IccColorSpaceType.CieXyz, 1F, 1F)]
    [InlineData(IccColorSpaceType.CieLab, 0F, 0F)]
    [InlineData(IccColorSpaceType.CieLab, 0.5F, 0.25F)]
    [InlineData(IccColorSpaceType.CieLab, 1F, 1F)]
    internal void ToPcs_ProducesNeutralColor(IccColorSpaceType pcsType, float gray, float connection)
    {
        GrayTrcCalculator calculator = new(new IccCurveTagDataEntry(2F), pcsType, toPcs: true);

        // Only the first device channel is meaningful. Distinct unused lanes detect accidental pass-through.
        Vector4 actual = calculator.Calculate(new Vector4(gray, 0.3F, 0.7F, 1F));
        Vector4 expected = pcsType == IccColorSpaceType.CieLab
            ? new CieLab(connection * 100F, 0, 0).ToScaledVector4()
            : new CieXyz(KnownIlluminants.D50Icc.ToVector3() * connection).ToScaledVector4();

        VectorAssert.Equal(expected, actual, 5);
    }

    /// <summary>
    /// Verifies that the inverse curve uses PCS luminance or lightness, independently of chromatic components.
    /// </summary>
    /// <param name="pcsType">The profile connection space.</param>
    /// <param name="connection">The normalized achromatic PCS value.</param>
    /// <param name="gray">The expected device gray value.</param>
    [Theory]
    [InlineData(IccColorSpaceType.CieXyz, 0F, 0F)]
    [InlineData(IccColorSpaceType.CieXyz, 0.25F, 0.5F)]
    [InlineData(IccColorSpaceType.CieXyz, 1F, 1F)]
    [InlineData(IccColorSpaceType.CieLab, 0F, 0F)]
    [InlineData(IccColorSpaceType.CieLab, 0.25F, 0.5F)]
    [InlineData(IccColorSpaceType.CieLab, 1F, 1F)]
    internal void FromPcs_UsesAchromaticComponent(IccColorSpaceType pcsType, float connection, float gray)
    {
        GrayTrcCalculator calculator = new(new IccCurveTagDataEntry(2F), pcsType, toPcs: false);

        // Use non-neutral inputs so selecting XYZ.X instead of XYZ.Y cannot pass as a round-trip would.
        Vector4 input = pcsType == IccColorSpaceType.CieLab
            ? new CieLab(connection * 100F, 10F, 20F).ToScaledVector4()
            : new CieXyz(0.3F, connection, 0.7F).ToScaledVector4();

        Assert.Equal(gray, calculator.Calculate(input).X, 5);
    }
}
