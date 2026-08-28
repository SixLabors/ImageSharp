// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies equal-weight AV1 compound prediction across every hardware-intrinsic tier.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CompoundInterPredictorTests
{
    /// <summary>
    /// Exercises the native vector width, 256-bit and 128-bit paths, and the complete scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies rounded 8-bit averaging, scalar tails, and untouched row padding under every SIMD configuration.
    /// </summary>
    [Fact]
    public void ByteAverageMatchesIndependentOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateByteAverage, PredictorConfigurations);

    /// <summary>
    /// Verifies rounded 10/12-bit averaging, scalar tails, and untouched row padding under every SIMD configuration.
    /// </summary>
    [Fact]
    public void HighBitDepthAverageMatchesIndependentOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthAverage, PredictorConfigurations);

    /// <summary>
    /// Applies independent byte arithmetic to block widths that cross every vector and scalar boundary.
    /// </summary>
    private static void ValidateByteAverage()
    {
        ReadOnlySpan<int> widths = [4, 7, 8, 15, 16, 23, 31, 32, 47, 64, 127, 128];

        foreach (int width in widths)
        {
            const int height = 5;
            int destinationStride = width + 11;
            int secondStride = width + 7;
            byte[] expected = new byte[destinationStride * height];
            byte[] actual = new byte[destinationStride * height];
            byte[] scalar = new byte[destinationStride * height];
            byte[] second = new byte[secondStride * height];

            FillByteInputs(expected, second, destinationStride, secondStride, width, height);
            expected.CopyTo(actual, 0);
            expected.CopyTo(scalar, 0);

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int destinationIndex = (row * destinationStride) + column;
                    int secondIndex = (row * secondStride) + column;
                    expected[destinationIndex] = (byte)((expected[destinationIndex] + second[secondIndex] + 1) >> 1);
                }
            }

            Av1CompoundInterPredictor.Average(actual, destinationStride, second, secondStride, width, height);
            Av1CompoundInterPredictor.AverageScalar(scalar, destinationStride, second, secondStride, width, height);

            Assert.Equal(expected, actual);
            Assert.Equal(expected, scalar);
        }
    }

    /// <summary>
    /// Applies independent ushort arithmetic at both supported high-bit-depth limits.
    /// </summary>
    private static void ValidateHighBitDepthAverage()
    {
        ReadOnlySpan<int> widths = [4, 7, 8, 15, 16, 23, 31, 32, 47, 64, 127, 128];

        foreach (int bitDepth in new[] { 10, 12 })
        {
            foreach (int width in widths)
            {
                const int height = 5;
                int destinationStride = width + 9;
                int secondStride = width + 5;
                ushort[] expected = new ushort[destinationStride * height];
                ushort[] actual = new ushort[destinationStride * height];
                ushort[] scalar = new ushort[destinationStride * height];
                ushort[] second = new ushort[secondStride * height];

                FillHighBitDepthInputs(expected, second, destinationStride, secondStride, width, height, bitDepth);
                expected.CopyTo(actual, 0);
                expected.CopyTo(scalar, 0);

                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        int destinationIndex = (row * destinationStride) + column;
                        int secondIndex = (row * secondStride) + column;
                        expected[destinationIndex] = (ushort)((expected[destinationIndex] + second[secondIndex] + 1) >> 1);
                    }
                }

                Av1CompoundInterPredictor.Average(actual, destinationStride, second, secondStride, width, height);
                Av1CompoundInterPredictor.AverageScalar(scalar, destinationStride, second, secondStride, width, height);

                Assert.Equal(expected, actual);
                Assert.Equal(expected, scalar);
            }
        }
    }

    /// <summary>
    /// Fills active byte samples while assigning different sentinels to the unused row tails.
    /// </summary>
    private static void FillByteInputs(
        Span<byte> destination,
        Span<byte> second,
        int destinationStride,
        int secondStride,
        int width,
        int height)
    {
        destination.Fill(0xD3);
        second.Fill(0xA7);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = (byte)((row * 47) + (column * 29) + 3);
                second[(row * secondStride) + column] = (byte)((row * 31) + (column * 53) + 11);
            }
        }
    }

    /// <summary>
    /// Fills active ushort samples across the requested precision while preserving guarded row tails.
    /// </summary>
    private static void FillHighBitDepthInputs(
        Span<ushort> destination,
        Span<ushort> second,
        int destinationStride,
        int secondStride,
        int width,
        int height,
        int bitDepth)
    {
        destination.Fill(0xDEAD);
        second.Fill(0xBEEF);
        int mask = (1 << bitDepth) - 1;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = (ushort)(((row * 947) + (column * 613) + 17) & mask);
                second[(row * secondStride) + column] = (ushort)(((row * 541) + (column * 887) + 23) & mask);
            }
        }
    }
}
