// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies affine warped-motion prediction through native SIMD and scalar execution.
/// </summary>
[Trait("Format", "Avif")]
public class Av1WarpedInterPredictorTests
{
    /// <summary>
    /// The hardware configurations covering the portable vector operator and scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies exact 8-bit vector/scalar parity for luma and subsampled chroma coordinates.
    /// </summary>
    [Fact]
    public void BytePredictionMatchesScalarAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateBytePrediction, PredictorConfigurations);

    /// <summary>
    /// Verifies exact 8-, 10-, and 12-bit vector/scalar parity for luma and subsampled chroma coordinates.
    /// </summary>
    [Fact]
    public void HighBitDepthPredictionMatchesScalarAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthPrediction, PredictorConfigurations);

    /// <summary>
    /// Applies the pinned multi-sample affine model to deterministic byte storage.
    /// </summary>
    private static void ValidateBytePrediction()
    {
        const int activeSize = 96;
        const int padding = 24;
        const int sourceStride = activeSize + (2 * padding);
        const int width = 13;
        const int height = 11;
        const int destinationStride = width + 7;
        byte[] source = new byte[sourceStride * sourceStride];
        for (int row = 0; row < sourceStride; row++)
        {
            for (int column = 0; column < sourceStride; column++)
            {
                source[(row * sourceStride) + column] = (byte)(((row * 29) + (column * 47) + (row * column * 3)) & byte.MaxValue);
            }
        }

        Av1GlobalMotionParameters parameters = CreatePinnedParameters();
        for (int subsampling = 0; subsampling <= 1; subsampling++)
        {
            byte[] expected = new byte[destinationStride * height];
            byte[] actual = new byte[destinationStride * height];
            Array.Fill(expected, (byte)0xD3);
            Array.Fill(actual, (byte)0xD3);
            short[] expectedScratch = new short[Av1InterPredictor.WarpedScratchLength];
            short[] actualScratch = new short[Av1InterPredictor.WarpedScratchLength];
            Point destinationPosition = subsampling == 0 ? new Point(32, 24) : new Point(16, 12);

            Av1InterPredictor.PredictWarpedScalar(
                source,
                sourceStride,
                new Point(padding, padding),
                activeSize,
                activeSize,
                expected,
                destinationStride,
                destinationPosition,
                width,
                height,
                subsampling,
                subsampling,
                parameters,
                expectedScratch);

            Av1InterPredictor.PredictWarped(
                source,
                sourceStride,
                new Point(padding, padding),
                activeSize,
                activeSize,
                actual,
                destinationStride,
                destinationPosition,
                width,
                height,
                subsampling,
                subsampling,
                parameters,
                actualScratch);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Applies the pinned multi-sample affine model to every supported high-bit-depth precision.
    /// </summary>
    private static void ValidateHighBitDepthPrediction()
    {
        const int activeSize = 96;
        const int padding = 24;
        const int sourceStride = activeSize + (2 * padding);
        const int width = 13;
        const int height = 11;
        const int destinationStride = width + 7;
        Av1GlobalMotionParameters parameters = CreatePinnedParameters();
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            ushort[] source = new ushort[sourceStride * sourceStride];
            for (int row = 0; row < sourceStride; row++)
            {
                for (int column = 0; column < sourceStride; column++)
                {
                    source[(row * sourceStride) + column] =
                        (ushort)(((row * 269) + (column * 443) + (row * column * 31)) & maximum);

                }
            }

            for (int subsampling = 0; subsampling <= 1; subsampling++)
            {
                ushort[] expected = new ushort[destinationStride * height];
                ushort[] actual = new ushort[destinationStride * height];
                Array.Fill(expected, (ushort)0xDEAD);
                Array.Fill(actual, (ushort)0xDEAD);
                short[] expectedScratch = new short[Av1InterPredictor.WarpedScratchLength];
                short[] actualScratch = new short[Av1InterPredictor.WarpedScratchLength];
                Point destinationPosition = subsampling == 0 ? new Point(32, 24) : new Point(16, 12);

                Av1InterPredictor.PredictWarpedScalar(
                    source,
                    sourceStride,
                    new Point(padding, padding),
                    activeSize,
                    activeSize,
                    expected,
                    destinationStride,
                    destinationPosition,
                    width,
                    height,
                    subsampling,
                    subsampling,
                    bitDepth,
                    parameters,
                    expectedScratch);

                Av1InterPredictor.PredictWarped(
                    source,
                    sourceStride,
                    new Point(padding, padding),
                    activeSize,
                    activeSize,
                    actual,
                    destinationStride,
                    destinationPosition,
                    width,
                    height,
                    subsampling,
                    subsampling,
                    bitDepth,
                    parameters,
                    actualScratch);

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Creates one nontrivial affine model traced from the pinned two-frame local-warp fixture.
    /// </summary>
    private static Av1GlobalMotionParameters CreatePinnedParameters()
    {
        Av1GlobalMotionParameters parameters = Av1GlobalMotionParameters.Identity;
        parameters.Type = Av1GlobalMotionType.Affine;
        parameters[0] = -191565;
        parameters[1] = 599107;
        parameters[2] = 61755;
        parameters[3] = -140;
        parameters[4] = -6909;
        parameters[5] = 62012;
        parameters.UpdateShearParameters();
        Assert.False(parameters.IsInvalid);
        Assert.Equal(-3776, parameters.Alpha);
        Assert.Equal(-128, parameters.Beta);
        Assert.Equal(-7360, parameters.Gamma);
        Assert.Equal(-3520, parameters.Delta);
        return parameters;
    }
}
