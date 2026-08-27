// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 intra-block-copy interpolation across the supported hardware-intrinsic configurations.
/// </summary>
[Trait("Format", "Heif")]
public class Av1IntraBlockCopyPredictorTests
{
    /// <summary>
    /// Exercises each SIMD register-width tier and the complete scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies all four source phases for 8-bit samples at every AV1 transform size.
    /// </summary>
    [Fact]
    public void EightBitPredictionMatchesScalarAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateEightBitPrediction, PredictorConfigurations);

    /// <summary>
    /// Verifies all four source phases for high-bit-depth samples at every AV1 transform size.
    /// </summary>
    [Fact]
    public void HighBitDepthPredictionMatchesScalarAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthPrediction, PredictorConfigurations);

    /// <summary>
    /// Verifies the four normative interpolation equations against independently calculated sample blocks.
    /// </summary>
    [Fact]
    public void PredictionMatchesKnownInterpolationValues()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateKnownInterpolationValues, PredictorConfigurations);

    /// <summary>
    /// Compares the SIMD-first 8-bit implementation with its scalar definition and verifies that row padding is unchanged.
    /// </summary>
    private static void ValidateEightBitPrediction()
    {
        for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)sizeIndex;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sourceStride = width + 17;
            int destinationStride = width + 7;
            byte[] source = new byte[sourceStride * (height + 1)];

            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)((i * 29) + 17);
            }

            for (int phase = 0; phase < 4; phase++)
            {
                byte[] expected = Enumerable.Repeat((byte)0xA5, destinationStride * height).ToArray();
                byte[] actual = Enumerable.Repeat((byte)0xA5, destinationStride * height).ToArray();
                bool halfX = (phase & 1) != 0;
                bool halfY = (phase & 2) != 0;

                Av1IntraBlockCopyPredictor.PredictScalar(
                    source,
                    sourceStride,
                    expected,
                    destinationStride,
                    width,
                    height,
                    halfX,
                    halfY);

                Av1IntraBlockCopyPredictor.Predict(
                    source,
                    sourceStride,
                    actual,
                    destinationStride,
                    width,
                    height,
                    halfX,
                    halfY);

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Compares the SIMD-first high-bit-depth implementation with its scalar definition and verifies exact-width stores.
    /// </summary>
    private static void ValidateHighBitDepthPrediction()
    {
        for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)sizeIndex;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int sourceStride = width + 9;
            int destinationStride = width + 5;
            short[] source = new short[sourceStride * (height + 1)];

            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (short)(((i * 53) + 31) & 0xFFF);
            }

            for (int phase = 0; phase < 4; phase++)
            {
                short[] expected = Enumerable.Repeat((short)0x5A5A, destinationStride * height).ToArray();
                short[] actual = Enumerable.Repeat((short)0x5A5A, destinationStride * height).ToArray();
                bool halfX = (phase & 1) != 0;
                bool halfY = (phase & 2) != 0;

                Av1IntraBlockCopyPredictor.PredictScalar(
                    source,
                    sourceStride,
                    expected,
                    destinationStride,
                    width,
                    height,
                    halfX,
                    halfY);

                Av1IntraBlockCopyPredictor.Predict(
                    source,
                    sourceStride,
                    actual,
                    destinationStride,
                    width,
                    height,
                    halfX,
                    halfY);

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Applies each source phase to a four-by-four block whose expected results are simple arithmetic progressions.
    /// </summary>
    private static void ValidateKnownInterpolationValues()
    {
        const int sourceStride = 21;
        byte[] source = new byte[sourceStride * 5];
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                source[(row * sourceStride) + column] = (byte)((row * 20) + (column * 4));
            }
        }

        ReadOnlySpan<byte> copied =
        [
            0, 4, 8, 12,
            20, 24, 28, 32,
            40, 44, 48, 52,
            60, 64, 68, 72,
        ];

        ReadOnlySpan<byte> horizontal =
        [
            2, 6, 10, 14,
            22, 26, 30, 34,
            42, 46, 50, 54,
            62, 66, 70, 74,
        ];

        ReadOnlySpan<byte> vertical =
        [
            10, 14, 18, 22,
            30, 34, 38, 42,
            50, 54, 58, 62,
            70, 74, 78, 82,
        ];

        ReadOnlySpan<byte> bilinear =
        [
            12, 16, 20, 24,
            32, 36, 40, 44,
            52, 56, 60, 64,
            72, 76, 80, 84,
        ];

        ValidateKnownPhase(source, sourceStride, copied, false, false);
        ValidateKnownPhase(source, sourceStride, horizontal, true, false);
        ValidateKnownPhase(source, sourceStride, vertical, false, true);
        ValidateKnownPhase(source, sourceStride, bilinear, true, true);
    }

    /// <summary>
    /// Verifies one four-by-four source phase for both 8-bit and translated high-bit-depth samples.
    /// </summary>
    /// <param name="source">The five-by-five 8-bit source region.</param>
    /// <param name="sourceStride">The number of source samples per row.</param>
    /// <param name="expected">The independently calculated four-by-four prediction.</param>
    /// <param name="halfX">Indicates whether the horizontal phase is one half-sample.</param>
    /// <param name="halfY">Indicates whether the vertical phase is one half-sample.</param>
    private static void ValidateKnownPhase(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> expected,
        bool halfX,
        bool halfY)
    {
        byte[] actual = new byte[16];
        Av1IntraBlockCopyPredictor.Predict(source, sourceStride, actual, 4, 4, 4, halfX, halfY);
        Assert.Equal(expected, actual);

        const int highBitDepthOffset = 1024;
        short[] highBitDepthSource = new short[source.Length];
        short[] highBitDepthExpected = new short[expected.Length];
        short[] highBitDepthActual = new short[16];

        for (int i = 0; i < source.Length; i++)
        {
            highBitDepthSource[i] = (short)(source[i] + highBitDepthOffset);
        }

        for (int i = 0; i < expected.Length; i++)
        {
            highBitDepthExpected[i] = (short)(expected[i] + highBitDepthOffset);
        }

        Av1IntraBlockCopyPredictor.Predict(highBitDepthSource, sourceStride, highBitDepthActual, 4, 4, 4, halfX, halfY);
        Assert.Equal(highBitDepthExpected, highBitDepthActual);
    }
}
