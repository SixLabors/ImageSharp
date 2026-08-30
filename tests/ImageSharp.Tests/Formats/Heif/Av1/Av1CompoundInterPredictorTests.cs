// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 compound prediction and blending across every hardware-intrinsic tier.
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
    /// Verifies 10/12-bit no-round prediction and compound finalization across every intrinsic width.
    /// </summary>
    [Fact]
    public void HighBitDepthCompoundIntermediatesMatchIndependentOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateHighBitDepthCompoundIntermediates,
            PredictorConfigurations);

    /// <summary>
    /// Verifies every current libaom display-distance quantization class in both temporal directions.
    /// </summary>
    /// <param name="firstOrderHint">The first reference order hint.</param>
    /// <param name="secondOrderHint">The second reference order hint.</param>
    /// <param name="expectedFirstWeight">The expected first predictor weight.</param>
    /// <param name="expectedSecondWeight">The expected second predictor weight.</param>
    [Theory]
    [InlineData(13, 20, 9, 7)]
    [InlineData(15, 18, 11, 5)]
    [InlineData(15, 19, 12, 4)]
    [InlineData(15, 20, 13, 3)]
    [InlineData(12, 19, 7, 9)]
    [InlineData(14, 17, 5, 11)]
    [InlineData(13, 17, 4, 12)]
    [InlineData(12, 17, 3, 13)]
    [InlineData(12, 16, 3, 13)]
    [InlineData(16, 20, 13, 3)]
    public void DistanceWeightsMatchCurrentLibaomQuantization(
        int firstOrderHint,
        int secondOrderHint,
        int expectedFirstWeight,
        int expectedSecondWeight)
    {
        ObuOrderHintInfo orderHintInfo = new()
        {
            EnableOrderHint = true,
            OrderHintBits = 5,
        };

        ObuFrameHeader frameHeader = new() { OrderHint = 16 };
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;
        frameHeader.GetReferenceOrderHints()[0] = (uint)firstOrderHint;
        frameHeader.GetReferenceOrderHints()[1] = (uint)secondOrderHint;

        Av1CompoundDistanceWeights.Derive(
            orderHintInfo,
            frameHeader,
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.Last2,
            out int firstWeight,
            out int secondWeight);

        Assert.Equal(expectedFirstWeight, firstWeight);
        Assert.Equal(expectedSecondWeight, secondWeight);
    }

    /// <summary>
    /// Verifies 8-bit distance and per-sample mask blending across every intrinsic width and scalar tail.
    /// </summary>
    [Fact]
    public void ByteSelectableBlendsMatchIndependentOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateByteSelectableBlends, PredictorConfigurations);

    /// <summary>
    /// Verifies 10/12-bit distance and per-sample mask blending across every intrinsic width and scalar tail.
    /// </summary>
    [Fact]
    public void HighBitDepthSelectableBlendsMatchIndependentOracleAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthSelectableBlends, PredictorConfigurations);

    /// <summary>
    /// Verifies the four smooth inter-intra modes and their complemented destination orientation.
    /// </summary>
    [Fact]
    public void SmoothInterIntraMasksMatchCurrentLibaomWeights()
    {
        ReadOnlySpan<byte> weights = [60, 34, 19, 11, 6, 4, 2, 1];

        foreach (Av1InterIntraMode mode in Enum.GetValues<Av1InterIntraMode>())
        {
            const int width = 8;
            const int height = 4;
            const int stride = 11;
            byte[] mask = new byte[stride * height];
            byte[] inverted = new byte[stride * height];
            mask.AsSpan().Fill(0xA5);
            inverted.AsSpan().Fill(0xA5);

            Av1InterIntraMaskBuilder.FillInterIntraMask(mask, stride, width, height, mode, invert: false);
            Av1InterIntraMaskBuilder.FillInterIntraMask(inverted, stride, width, height, mode, invert: true);

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    byte expected = mode switch
                    {
                        Av1InterIntraMode.Vertical => weights[row],
                        Av1InterIntraMode.Horizontal => weights[column],
                        Av1InterIntraMode.Smooth => weights[Math.Min(row, column)],
                        _ => 32,
                    };

                    Assert.Equal(expected, mask[(row * stride) + column]);
                    Assert.Equal((byte)(64 - expected), inverted[(row * stride) + column]);
                }

                for (int column = width; column < stride; column++)
                {
                    Assert.Equal(0xA5, mask[(row * stride) + column]);
                    Assert.Equal(0xA5, inverted[(row * stride) + column]);
                }
            }
        }
    }

    /// <summary>
    /// Verifies the current libaom horizontal curve at the index exercised by a 32-by-16 inter-intra block.
    /// </summary>
    [Fact]
    public void HorizontalInterIntraMaskMatchesCurrentLibaomThirtyTwoWideCurve()
    {
        const int width = 32;
        const int height = 16;
        byte[] mask = new byte[width * height];
        byte[] inverted = new byte[width * height];

        Av1InterIntraMaskBuilder.FillInterIntraMask(mask, width, width, height, Av1InterIntraMode.Horizontal, invert: false);
        Av1InterIntraMaskBuilder.FillInterIntraMask(inverted, width, width, height, Av1InterIntraMode.Horizontal, invert: true);

        Assert.Equal(2, mask[23]);
        Assert.Equal(62, inverted[23]);
    }

    /// <summary>
    /// Verifies both difference-mask orientations at each supported bit depth.
    /// </summary>
    [Fact]
    public void DifferenceWeightedMasksMatchPinnedFormula()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateDifferenceWeightedMasks, PredictorConfigurations);

    /// <summary>
    /// Applies the pinned difference-mask formula at every bit depth and intrinsic width.
    /// </summary>
    private static void ValidateDifferenceWeightedMasks()
    {
        ReadOnlySpan<int> widths = [9, 16, 23, 32, 47, 64, 127];

        foreach (int width in widths)
        {
            const int height = 3;
            int firstStride = width + 4;
            int secondStride = width + 2;
            int maskStride = width + 3;
            foreach (int bitDepth in new[] { 8, 10, 12 })
            {
                int sampleMask = (1 << bitDepth) - 1;
                ushort[] first = new ushort[firstStride * height];
                ushort[] second = new ushort[secondStride * height];
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        first[(row * firstStride) + column] = (ushort)(((row * 911) + (column * 521)) & sampleMask);
                        second[(row * secondStride) + column] = (ushort)(((row * 307) + (column * 997) + 31) & sampleMask);
                    }
                }

                foreach (Av1DifferenceWeightedMaskType maskType in Enum.GetValues<Av1DifferenceWeightedMaskType>())
                {
                    byte[] actual = new byte[maskStride * height];
                    actual.AsSpan().Fill(0xA5);

                    if (bitDepth == 8)
                    {
                        byte[] firstByte = Array.ConvertAll(first, value => (byte)value);
                        byte[] secondByte = Array.ConvertAll(second, value => (byte)value);
                        Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                            actual,
                            maskStride,
                            firstByte,
                            firstStride,
                            secondByte,
                            secondStride,
                            width,
                            height,
                            maskType);
                    }
                    else
                    {
                        Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                            actual,
                            maskStride,
                            first,
                            firstStride,
                            second,
                            secondStride,
                            width,
                            height,
                            bitDepth,
                            maskType);
                    }

                    for (int row = 0; row < height; row++)
                    {
                        for (int column = 0; column < width; column++)
                        {
                            int difference = Math.Abs(first[(row * firstStride) + column] - second[(row * secondStride) + column]);
                            int alpha = Math.Min(64, 38 + ((difference >> (bitDepth - 8)) / 16));
                            byte expected = (byte)(maskType == Av1DifferenceWeightedMaskType.Type38Inverse ? 64 - alpha : alpha);

                            Assert.Equal(expected, actual[(row * maskStride) + column]);
                        }

                        for (int column = width; column < maskStride; column++)
                        {
                            Assert.Equal(0xA5, actual[(row * maskStride) + column]);
                        }
                    }
                }
            }
        }
    }

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

            Av1CompoundAveragePredictor.Average(actual, destinationStride, second, secondStride, width, height);
            Av1CompoundAveragePredictor.AverageScalar(scalar, destinationStride, second, secondStride, width, height);

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

                Av1CompoundAveragePredictor.Average(actual, destinationStride, second, secondStride, width, height);
                Av1CompoundAveragePredictor.AverageScalar(scalar, destinationStride, second, secondStride, width, height);

                Assert.Equal(expected, actual);
                Assert.Equal(expected, scalar);
            }
        }
    }

    /// <summary>
    /// Applies the high-bit-depth no-round convolution equations independently of the production operators.
    /// </summary>
    private static void ValidateHighBitDepthCompoundIntermediates()
    {
        ReadOnlySpan<int> widths = [9, 17, 33, 65];
        ReadOnlySpan<(int Horizontal, int Vertical)> phases =
            [(0, 0), (5, 0), (0, 9), (5, 9)];

        foreach (int bitDepth in new[] { 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            int intermediateRange = bitDepth + 7 - 3 + 2;
            int round0 = 3 + Math.Max(intermediateRange - 16, 0);
            int roundBits = 14 - round0 - 7;
            int offsetBits = bitDepth + 14 - round0;
            int roundOffset = (1 << (offsetBits - 7)) + (1 << (offsetBits - 8));

            foreach (int width in widths)
            {
                const int height = 3;
                int sourceStride = width + 5;
                int intermediateStride = width + 3;
                int destinationStride = width + 7;
                ushort[] firstSource = new ushort[sourceStride * (height + 1)];
                ushort[] secondSource = new ushort[sourceStride * (height + 1)];

                for (int row = 0; row <= height; row++)
                {
                    for (int column = 0; column < sourceStride; column++)
                    {
                        firstSource[(row * sourceStride) + column] =
                            (ushort)(((row * 613) + (column * 349) + 17) & maximum);

                        secondSource[(row * sourceStride) + column] =
                            (ushort)(((row * 947) + (column * 181) + 71) & maximum);
                    }
                }

                foreach ((int horizontalPhase, int verticalPhase) in phases)
                {
                    int horizontal0 = 128 - (horizontalPhase * 8);
                    int horizontal1 = horizontalPhase * 8;
                    int vertical0 = 128 - (verticalPhase * 8);
                    int vertical1 = verticalPhase * 8;
                    ushort[] expectedFirst = new ushort[intermediateStride * height];
                    ushort[] expectedSecond = new ushort[intermediateStride * height];
                    ushort[] actualFirst = new ushort[intermediateStride * height];
                    ushort[] actualSecond = new ushort[intermediateStride * height];
                    ushort[] scalarFirst = new ushort[intermediateStride * height];
                    ushort[] scalarSecond = new ushort[intermediateStride * height];
                    expectedFirst.AsSpan().Fill(0xA5A5);
                    expectedSecond.AsSpan().Fill(0xA5A5);
                    actualFirst.AsSpan().Fill(0xA5A5);
                    actualSecond.AsSpan().Fill(0xA5A5);
                    scalarFirst.AsSpan().Fill(0xA5A5);
                    scalarSecond.AsSpan().Fill(0xA5A5);

                    for (int predictorIndex = 0; predictorIndex < 2; predictorIndex++)
                    {
                        ReadOnlySpan<ushort> source = predictorIndex == 0 ? firstSource : secondSource;
                        Span<ushort> expected = predictorIndex == 0 ? expectedFirst : expectedSecond;

                        for (int row = 0; row < height; row++)
                        {
                            for (int column = 0; column < width; column++)
                            {
                                int sourceIndex = (row * sourceStride) + column;
                                int result;
                                if (horizontalPhase == 0 && verticalPhase == 0)
                                {
                                    result = (source[sourceIndex] << roundBits) + roundOffset;
                                }
                                else if (verticalPhase == 0)
                                {
                                    int sum = (horizontal0 * source[sourceIndex]) +
                                        (horizontal1 * source[sourceIndex + 1]);

                                    result = ((sum + (1 << (round0 - 1))) >> round0) + roundOffset;
                                }
                                else if (horizontalPhase == 0)
                                {
                                    int sum = (vertical0 * source[sourceIndex]) +
                                        (vertical1 * source[sourceIndex + sourceStride]);

                                    int shifted = sum << (7 - round0);
                                    result = ((shifted + 64) >> 7) + roundOffset;
                                }
                                else
                                {
                                    int horizontalBias = 1 << (bitDepth + 6);
                                    int firstHorizontal = horizontalBias +
                                        (horizontal0 * source[sourceIndex]) +
                                        (horizontal1 * source[sourceIndex + 1]);

                                    int secondHorizontal = horizontalBias +
                                        (horizontal0 * source[sourceIndex + sourceStride]) +
                                        (horizontal1 * source[sourceIndex + sourceStride + 1]);

                                    firstHorizontal = (firstHorizontal + (1 << (round0 - 1))) >> round0;
                                    secondHorizontal = (secondHorizontal + (1 << (round0 - 1))) >> round0;
                                    int verticalBias = 1 << (bitDepth + 14 - round0);
                                    int vertical = verticalBias +
                                        (vertical0 * firstHorizontal) +
                                        (vertical1 * secondHorizontal);

                                    result = (vertical + 64) >> 7;
                                }

                                expected[(row * intermediateStride) + column] = (ushort)result;
                            }
                        }
                    }

                    int scratchStride = Math.Max(width, 128);
                    short[] scratch = new short[scratchStride * (height + 8)];
                    Av1CompoundInterPredictor.PredictCompound(
                        firstSource,
                        sourceStride,
                        sourceOrigin: 0,
                        actualFirst,
                        intermediateStride,
                        width,
                        height,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        horizontalPhase,
                        verticalPhase,
                        bitDepth,
                        scratch);

                    Av1CompoundInterPredictor.PredictCompound(
                        secondSource,
                        sourceStride,
                        sourceOrigin: 0,
                        actualSecond,
                        intermediateStride,
                        width,
                        height,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        horizontalPhase,
                        verticalPhase,
                        bitDepth,
                        scratch);

                    Av1CompoundInterPredictor.PredictCompoundScalar(
                        firstSource,
                        sourceStride,
                        sourceOrigin: 0,
                        scalarFirst,
                        intermediateStride,
                        width,
                        height,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        horizontalPhase,
                        verticalPhase,
                        bitDepth,
                        scratch);

                    Av1CompoundInterPredictor.PredictCompoundScalar(
                        secondSource,
                        sourceStride,
                        sourceOrigin: 0,
                        scalarSecond,
                        intermediateStride,
                        width,
                        height,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        horizontalPhase,
                        verticalPhase,
                        bitDepth,
                        scratch);

                    Assert.Equal(expectedFirst, actualFirst);
                    Assert.Equal(expectedSecond, actualSecond);
                    Assert.Equal(expectedFirst, scalarFirst);
                    Assert.Equal(expectedSecond, scalarSecond);

                    ushort[] expectedDestination = new ushort[destinationStride * height];
                    ushort[] actualDestination = new ushort[destinationStride * height];
                    expectedDestination.AsSpan().Fill(0xA5A5);
                    actualDestination.AsSpan().Fill(0xA5A5);

                    for (int row = 0; row < height; row++)
                    {
                        for (int column = 0; column < width; column++)
                        {
                            int intermediateIndex = (row * intermediateStride) + column;
                            int result = ((expectedFirst[intermediateIndex] + expectedSecond[intermediateIndex]) >> 1) -
                                roundOffset;

                            result = (result + (1 << (roundBits - 1))) >> roundBits;
                            expectedDestination[(row * destinationStride) + column] =
                                (ushort)Math.Clamp(result, 0, maximum);
                        }
                    }

                    Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                        actualDestination,
                        destinationStride,
                        actualFirst,
                        intermediateStride,
                        actualSecond,
                        intermediateStride,
                        width,
                        height,
                        bitDepth);

                    Assert.Equal(expectedDestination, actualDestination);

                    ReadOnlySpan<int> distanceWeights = [9, 7, 11, 5, 12, 4, 13, 3];
                    for (int weightIndex = 0; weightIndex < distanceWeights.Length; weightIndex += 2)
                    {
                        ushort[] expectedWeighted = new ushort[destinationStride * height];
                        ushort[] actualWeighted = new ushort[destinationStride * height];
                        expectedWeighted.AsSpan().Fill(0xA5A5);
                        actualWeighted.AsSpan().Fill(0xA5A5);
                        int firstWeight = distanceWeights[weightIndex];
                        int secondWeight = distanceWeights[weightIndex + 1];

                        for (int row = 0; row < height; row++)
                        {
                            for (int column = 0; column < width; column++)
                            {
                                int intermediateIndex = (row * intermediateStride) + column;
                                int result = ((expectedFirst[intermediateIndex] * firstWeight) +
                                    (expectedSecond[intermediateIndex] * secondWeight)) >> 4;

                                result -= roundOffset;
                                result = (result + (1 << (roundBits - 1))) >> roundBits;
                                expectedWeighted[(row * destinationStride) + column] =
                                    (ushort)Math.Clamp(result, 0, maximum);
                            }
                        }

                        Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                            actualWeighted,
                            destinationStride,
                            actualFirst,
                            intermediateStride,
                            actualSecond,
                            intermediateStride,
                            width,
                            height,
                            firstWeight,
                            secondWeight,
                            bitDepth);

                        Assert.Equal(expectedWeighted, actualWeighted);
                    }

                    int maskStride = width + 5;
                    byte[] mask = new byte[maskStride * height];
                    ushort[] expectedMasked = new ushort[destinationStride * height];
                    ushort[] actualMasked = new ushort[destinationStride * height];
                    expectedMasked.AsSpan().Fill(0xA5A5);
                    actualMasked.AsSpan().Fill(0xA5A5);

                    for (int row = 0; row < height; row++)
                    {
                        for (int column = 0; column < width; column++)
                        {
                            byte alpha = (byte)(((row * 29) + (column * 17) + 3) % 65);
                            mask[(row * maskStride) + column] = alpha;
                            int intermediateIndex = (row * intermediateStride) + column;
                            int result = ((alpha * expectedFirst[intermediateIndex]) +
                                ((64 - alpha) * expectedSecond[intermediateIndex])) >> 6;

                            result -= roundOffset;
                            result = (result + (1 << (roundBits - 1))) >> roundBits;
                            expectedMasked[(row * destinationStride) + column] =
                                (ushort)Math.Clamp(result, 0, maximum);
                        }
                    }

                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                        actualMasked,
                        destinationStride,
                        actualFirst,
                        intermediateStride,
                        actualSecond,
                        intermediateStride,
                        mask,
                        maskStride,
                        width,
                        height,
                        subX: 0,
                        subY: 0,
                        bitDepth);

                    Assert.Equal(expectedMasked, actualMasked);
                }
            }
        }
    }

    /// <summary>
    /// Applies independent byte arithmetic to every selectable compound blend.
    /// </summary>
    private static void ValidateByteSelectableBlends()
    {
        ReadOnlySpan<int> widths = [4, 7, 8, 15, 16, 23, 31, 32, 47, 64, 127, 128];
        ReadOnlySpan<int> distanceWeights = [9, 7, 11, 5, 12, 4, 13, 3];

        foreach (int width in widths)
        {
            const int height = 5;
            int destinationStride = width + 11;
            int secondStride = width + 7;
            int maskStride = width + 5;
            byte[] first = new byte[destinationStride * height];
            byte[] second = new byte[secondStride * height];
            byte[] mask = new byte[maskStride * height];

            FillByteInputs(first, second, destinationStride, secondStride, width, height);
            FillMask(mask, maskStride, width, height);

            for (int weightIndex = 0; weightIndex < distanceWeights.Length; weightIndex += 2)
            {
                byte[] expected = (byte[])first.Clone();
                byte[] actual = (byte[])first.Clone();
                int firstWeight = distanceWeights[weightIndex];
                int secondWeight = distanceWeights[weightIndex + 1];

                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        int destinationIndex = (row * destinationStride) + column;
                        int secondIndex = (row * secondStride) + column;
                        expected[destinationIndex] = (byte)(((expected[destinationIndex] * firstWeight) +
                            (second[secondIndex] * secondWeight) + 8) >> 4);
                    }
                }

                Av1CompoundDistanceWeightedPredictor.DistanceWeighted(
                    actual,
                    destinationStride,
                    second,
                    secondStride,
                    width,
                    height,
                    firstWeight,
                    secondWeight);

                Assert.Equal(expected, actual);
            }

            byte[] maskedExpected = (byte[])first.Clone();
            byte[] maskedActual = (byte[])first.Clone();
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int destinationIndex = (row * destinationStride) + column;
                    int secondIndex = (row * secondStride) + column;
                    int alpha = mask[(row * maskStride) + column];
                    maskedExpected[destinationIndex] = (byte)(((alpha * maskedExpected[destinationIndex]) +
                        ((64 - alpha) * second[secondIndex]) + 32) >> 6);
                }
            }

            Av1CompoundMaskBlendPredictor.Blend(
                maskedActual,
                destinationStride,
                second,
                secondStride,
                mask,
                maskStride,
                width,
                height);

            Assert.Equal(maskedExpected, maskedActual);
        }
    }

    /// <summary>
    /// Applies independent high-bit-depth arithmetic to every selectable compound blend.
    /// </summary>
    private static void ValidateHighBitDepthSelectableBlends()
    {
        ReadOnlySpan<int> widths = [4, 7, 8, 15, 16, 23, 31, 32, 47, 64, 127, 128];
        ReadOnlySpan<int> distanceWeights = [9, 7, 11, 5, 12, 4, 13, 3];

        foreach (int bitDepth in new[] { 10, 12 })
        {
            foreach (int width in widths)
            {
                const int height = 5;
                int destinationStride = width + 9;
                int secondStride = width + 5;
                int maskStride = width + 3;
                ushort[] first = new ushort[destinationStride * height];
                ushort[] second = new ushort[secondStride * height];
                byte[] mask = new byte[maskStride * height];

                FillHighBitDepthInputs(first, second, destinationStride, secondStride, width, height, bitDepth);
                FillMask(mask, maskStride, width, height);

                for (int weightIndex = 0; weightIndex < distanceWeights.Length; weightIndex += 2)
                {
                    ushort[] expected = (ushort[])first.Clone();
                    ushort[] actual = (ushort[])first.Clone();
                    int firstWeight = distanceWeights[weightIndex];
                    int secondWeight = distanceWeights[weightIndex + 1];

                    for (int row = 0; row < height; row++)
                    {
                        for (int column = 0; column < width; column++)
                        {
                            int destinationIndex = (row * destinationStride) + column;
                            int secondIndex = (row * secondStride) + column;
                            expected[destinationIndex] = (ushort)(((expected[destinationIndex] * firstWeight) +
                                (second[secondIndex] * secondWeight) + 8) >> 4);
                        }
                    }

                    Av1CompoundDistanceWeightedPredictor.DistanceWeighted(
                        actual,
                        destinationStride,
                        second,
                        secondStride,
                        width,
                        height,
                        firstWeight,
                        secondWeight);

                    Assert.Equal(expected, actual);
                }

                ushort[] maskedExpected = (ushort[])first.Clone();
                ushort[] maskedActual = (ushort[])first.Clone();
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        int destinationIndex = (row * destinationStride) + column;
                        int secondIndex = (row * secondStride) + column;
                        int alpha = mask[(row * maskStride) + column];
                        maskedExpected[destinationIndex] = (ushort)(((alpha * maskedExpected[destinationIndex]) +
                            ((64 - alpha) * second[secondIndex]) + 32) >> 6);
                    }
                }

                Av1CompoundMaskBlendPredictor.Blend(
                    maskedActual,
                    destinationStride,
                    second,
                    secondStride,
                    mask,
                    maskStride,
                    width,
                    height);

                Assert.Equal(maskedExpected, maskedActual);
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

    /// <summary>
    /// Fills active mask samples across the complete AV1 alpha range while guarding every row tail.
    /// </summary>
    private static void FillMask(Span<byte> mask, int maskStride, int width, int height)
    {
        mask.Fill(0xA5);
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                mask[(row * maskStride) + column] = (byte)(((row * 19) + (column * 37)) % 65);
            }
        }
    }
}
