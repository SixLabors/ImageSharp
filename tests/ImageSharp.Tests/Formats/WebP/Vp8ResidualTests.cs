// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class Vp8ResidualTests
{
    private static void RunSetCoeffsTest()
    {
        // arrange
        var residual = new Vp8Residual();
        short[] coeffs = { 110, 0, -2, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0 };

        // act
        residual.SetCoeffs(coeffs);

        // assert
        Assert.Equal(9, residual.Last);
    }

    [Fact]
    public void RunSetCoeffsTest_Works() => RunSetCoeffsTest();

    [Fact]
    public void RunSetCoeffsTest_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSetCoeffsTest, HwIntrinsics.AllowAll);

    [Fact]
    public void RunSetCoeffsTest_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSetCoeffsTest, HwIntrinsics.DisableHWIntrinsic);
}
