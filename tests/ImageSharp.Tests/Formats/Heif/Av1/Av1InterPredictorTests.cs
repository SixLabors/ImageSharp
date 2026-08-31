// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 translational inter prediction against an independent implementation of the normative fixed-point convolution rules.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterPredictorTests
{
    /// <summary>
    /// The number of fractional coefficient bits in AV1 interpolation kernels.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The default first convolution shift used for 8- and 10-bit predictions.
    /// </summary>
    private const int Round0Bits = 3;

    /// <summary>
    /// The number of stored coefficient positions in every tested interpolation kernel.
    /// </summary>
    private const int FilterTapCount = 8;

    /// <summary>
    /// The number of coefficient positions preceding the integer-position sample.
    /// </summary>
    private const int FilterCenterOffset = 3;

    /// <summary>
    /// The source samples retained before the integer-position column.
    /// </summary>
    private const int SourceLeftPadding = 3;

    /// <summary>
    /// The source samples retained after each active row for a complete 512-bit byte load.
    /// </summary>
    private const int SourceRightPadding = 64;

    /// <summary>
    /// The source rows retained before the integer-position row.
    /// </summary>
    private const int SourceTopPadding = 3;

    /// <summary>
    /// The source rows retained after the prediction block.
    /// </summary>
    private const int SourceBottomPadding = 4;

    /// <summary>
    /// The guarded destination elements preceding the first active row.
    /// </summary>
    private const int DestinationPrefix = 11;

    /// <summary>
    /// The guarded destination elements following the final padded row.
    /// </summary>
    private const int DestinationSuffix = 17;

    /// <summary>
    /// The guarded destination elements following each active row.
    /// </summary>
    private const int DestinationRowPadding = 13;

    /// <summary>
    /// The non-image value stored in every guarded 8-bit destination element.
    /// </summary>
    private const byte ByteDestinationSentinel = 0xD3;

    /// <summary>
    /// The non-image value stored in every guarded ushort destination element.
    /// </summary>
    private const ushort HighBitDepthDestinationSentinel = 0xDEAD;

    /// <summary>
    /// Exercises the native vector width, the 256-bit path, the 128-bit path, and the complete scalar fallback.
    /// </summary>
    /// <remarks>
    /// Disabling AVX also disables AVX2 and leaves the x86 128-bit vector tier enabled, which is the established
    /// <see cref="FeatureTestRunner"/> configuration used by the other AV1 SIMD tests.
    /// </remarks>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies exact 8-bit copy and convolution output, scalar tails, and untouched destination padding under every SIMD configuration.
    /// </summary>
    [Fact]
    public void BytePredictionMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateBytePredictions, PredictorConfigurations);

    /// <summary>
    /// Verifies exact 8-, 10-, and 12-bit ushort output, scalar tails, and untouched destination padding under every SIMD configuration.
    /// </summary>
    [Fact]
    public void HighBitDepthPredictionMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthPredictions, PredictorConfigurations);

    /// <summary>
    /// Verifies that SIMD compound intermediates retain scalar-equivalent values and untouched destination padding.
    /// </summary>
    [Fact]
    public void CompoundPredictionMatchesScalarAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateCompoundPredictions, PredictorConfigurations);

    /// <summary>
    /// Applies every byte prediction scenario to the SIMD-first and explicitly scalar entry points.
    /// </summary>
    private static void ValidateBytePredictions()
    {
        foreach (PredictionCase testCase in CreatePredictionCases())
        {
            byte[] source = CreateByteSource(testCase, out int sourceStride, out int sourceOrigin);
            int destinationStride = testCase.Width + DestinationRowPadding;
            byte[] expected = CreateByteDestination(testCase, destinationStride);
            byte[] actual = (byte[])expected.Clone();
            byte[] scalar = (byte[])expected.Clone();
            short[] simdScratch = CreateScratch(testCase);
            short[] scalarScratch = CreateScratch(testCase);

            ApplyReference(source, sourceStride, sourceOrigin, expected, DestinationPrefix, destinationStride, testCase, 8);

            Av1InterPredictor.Predict(
                source, sourceStride, sourceOrigin, actual.AsSpan(DestinationPrefix), destinationStride, testCase.Width, testCase.Height,
                testCase.HorizontalFilter, testCase.VerticalFilter, testCase.HorizontalPhase, testCase.VerticalPhase, simdScratch);

            Av1InterPredictor.PredictScalar(
                source, sourceStride, sourceOrigin, scalar.AsSpan(DestinationPrefix), destinationStride, testCase.Width, testCase.Height,
                testCase.HorizontalFilter, testCase.VerticalFilter, testCase.HorizontalPhase, testCase.VerticalPhase, scalarScratch);

            AssertEqual(expected, actual, testCase, "SIMD-first byte");
            AssertEqual(expected, scalar, testCase, "scalar byte");
        }
    }

    /// <summary>
    /// Applies every ushort prediction scenario at each supported sample precision to the SIMD-first and scalar entry points.
    /// </summary>
    private static void ValidateHighBitDepthPredictions()
    {
        int[] bitDepths = [8, 10, 12];
        foreach (int bitDepth in bitDepths)
        {
            foreach (PredictionCase testCase in CreatePredictionCases())
            {
                ushort[] source = CreateHighBitDepthSource(testCase, bitDepth, out int sourceStride, out int sourceOrigin);
                int destinationStride = testCase.Width + DestinationRowPadding;
                ushort[] expected = CreateHighBitDepthDestination(testCase, destinationStride);
                ushort[] actual = (ushort[])expected.Clone();
                ushort[] scalar = (ushort[])expected.Clone();
                short[] simdScratch = CreateScratch(testCase);
                short[] scalarScratch = CreateScratch(testCase);

                ApplyReference(source, sourceStride, sourceOrigin, expected, DestinationPrefix, destinationStride, testCase, bitDepth);

                Av1InterPredictor.Predict(
                    source, sourceStride, sourceOrigin, actual.AsSpan(DestinationPrefix), destinationStride, testCase.Width, testCase.Height,
                    testCase.HorizontalFilter, testCase.VerticalFilter, testCase.HorizontalPhase, testCase.VerticalPhase, bitDepth, simdScratch);

                Av1InterPredictor.PredictScalar(
                    source, sourceStride, sourceOrigin, scalar.AsSpan(DestinationPrefix), destinationStride, testCase.Width, testCase.Height,
                    testCase.HorizontalFilter, testCase.VerticalFilter, testCase.HorizontalPhase, testCase.VerticalPhase, bitDepth, scalarScratch);

                AssertEqual(expected, actual, testCase, $"SIMD-first {bitDepth}-bit ushort");
                AssertEqual(expected, scalar, testCase, $"scalar {bitDepth}-bit ushort");
            }
        }
    }

    /// <summary>
    /// Applies every byte prediction scenario to the SIMD-first and scalar compound-intermediate entry points.
    /// </summary>
    private static void ValidateCompoundPredictions()
    {
        foreach (PredictionCase testCase in CreatePredictionCases())
        {
            byte[] source = CreateByteSource(testCase, out int sourceStride, out int sourceOrigin);
            int destinationStride = testCase.Width + DestinationRowPadding;
            ushort[] expected = CreateHighBitDepthDestination(testCase, destinationStride);
            ushort[] actual = (ushort[])expected.Clone();
            short[] simdScratch = CreateScratch(testCase);
            short[] scalarScratch = CreateScratch(testCase);

            Av1CompoundInterPredictor.PredictCompoundScalar(
                source,
                sourceStride,
                sourceOrigin,
                expected.AsSpan(DestinationPrefix),
                destinationStride,
                testCase.Width,
                testCase.Height,
                testCase.HorizontalFilter,
                testCase.VerticalFilter,
                testCase.HorizontalPhase,
                testCase.VerticalPhase,
                scalarScratch);

            Av1CompoundInterPredictor.PredictCompound(
                source,
                sourceStride,
                sourceOrigin,
                actual.AsSpan(DestinationPrefix),
                destinationStride,
                testCase.Width,
                testCase.Height,
                testCase.HorizontalFilter,
                testCase.VerticalFilter,
                testCase.HorizontalPhase,
                testCase.VerticalPhase,
                simdScratch);

            AssertEqual(expected, actual, testCase, "SIMD-first compound intermediate");
        }
    }

    /// <summary>
    /// Creates the named operation matrix covering copy, each one-dimensional direction, separable filtering, reduced kernels, and vector tails.
    /// </summary>
    /// <returns>The prediction scenarios.</returns>
    private static PredictionCase[] CreatePredictionCases() =>
    [
            new("copy-sub8x8-chroma", 2, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Sharp, 0, 0),
            new("regular-horizontal-sub8x8-chroma", 2, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 3, 0),
            new("regular-vertical-sub8x8-chroma", 4, 2, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 0, 3),
            new("smooth-sharp-sub8x8-chroma", 2, 2, Av1InterpolationFilter.Smooth, Av1InterpolationFilter.Sharp, 7, 13),
            new("copy-wide-tail", 68, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Sharp, 0, 0),
            new("regular-horizontal-wide-tail", 68, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 1, 0),
            new("smooth-horizontal-256-tail", 36, 8, Av1InterpolationFilter.Smooth, Av1InterpolationFilter.Regular, 7, 0),
            new("sharp-horizontal-128-tail", 20, 8, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Regular, 8, 0),
            new("bilinear-horizontal", 8, 8, Av1InterpolationFilter.Bilinear, Av1InterpolationFilter.Regular, 15, 0),
            new("regular-horizontal-reduced", 4, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 3, 0),
            new("smooth-horizontal-reduced", 4, 8, Av1InterpolationFilter.Smooth, Av1InterpolationFilter.Regular, 13, 0),
            new("sharp-horizontal-reduced", 4, 8, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Regular, 5, 0),
            new("regular-vertical-reduced", 68, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 0, 3),
            new("smooth-vertical-reduced", 36, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Smooth, 0, 13),
            new("sharp-vertical-reduced", 20, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Sharp, 0, 5),
            new("bilinear-vertical", 8, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Bilinear, 0, 8),
            new("regular-smooth-two-dimensional", 68, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Smooth, 1, 15),
            new("sharp-bilinear-two-dimensional", 36, 8, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Bilinear, 8, 3),
            new("smooth-sharp-reduced-two-dimensional", 4, 4, Av1InterpolationFilter.Smooth, Av1InterpolationFilter.Sharp, 7, 13),
            new("bilinear-regular-small-height", 20, 4, Av1InterpolationFilter.Bilinear, Av1InterpolationFilter.Regular, 11, 5),
            new("sharp-smooth-small-width", 4, 8, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Smooth, 5, 7)
    ];

    /// <summary>
    /// Creates an 8-bit padded reference plane and returns the integer-position source origin within that plane.
    /// </summary>
    /// <param name="testCase">The prediction geometry used to size the plane.</param>
    /// <param name="sourceStride">Receives the padded source-row stride.</param>
    /// <param name="sourceOrigin">Receives the integer-position sample index.</param>
    /// <returns>The complete padded source plane.</returns>
    private static byte[] CreateByteSource(PredictionCase testCase, out int sourceStride, out int sourceOrigin)
    {
        sourceStride = SourceLeftPadding + testCase.Width + SourceRightPadding;
        int sourceHeight = SourceTopPadding + testCase.Height + SourceBottomPadding;
        byte[] source = new byte[sourceStride * sourceHeight];

        for (int row = 0; row < sourceHeight; row++)
        {
            for (int column = 0; column < sourceStride; column++)
            {
                // Distinct row, column, and cross-term multipliers prevent a wrong stride or tap direction from
                // producing the same arithmetic progression as the correctly addressed source window.
                source[(row * sourceStride) + column] = (byte)(((row * 59) + (column * 37) + (row * column * 11) + 17) & byte.MaxValue);
            }
        }

        sourceOrigin = (SourceTopPadding * sourceStride) + SourceLeftPadding;
        return source;
    }

    /// <summary>
    /// Creates a padded high-bit-depth reference plane spanning the legal range for the requested precision.
    /// </summary>
    /// <param name="testCase">The prediction geometry used to size the plane.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    /// <param name="sourceStride">Receives the padded source-row stride.</param>
    /// <param name="sourceOrigin">Receives the integer-position sample index.</param>
    /// <returns>The complete padded source plane.</returns>
    private static ushort[] CreateHighBitDepthSource(PredictionCase testCase, int bitDepth, out int sourceStride, out int sourceOrigin)
    {
        sourceStride = SourceLeftPadding + testCase.Width + SourceRightPadding;
        int sourceHeight = SourceTopPadding + testCase.Height + SourceBottomPadding;
        int maximum = (1 << bitDepth) - 1;
        ushort[] source = new ushort[sourceStride * sourceHeight];

        for (int row = 0; row < sourceHeight; row++)
        {
            for (int column = 0; column < sourceStride; column++)
            {
                // The high-bit-depth pattern uses different coprime multipliers and spans the complete requested
                // range, exercising negative-lobe clipping as well as low and high sample values.
                source[(row * sourceStride) + column] = (ushort)(((row * 977) + (column * 353) + (row * column * 29) + 101) & maximum);
            }
        }

        sourceOrigin = (SourceTopPadding * sourceStride) + SourceLeftPadding;
        return source;
    }

    /// <summary>
    /// Creates an 8-bit destination whose prefix, row padding, and suffix expose stores outside the prediction block.
    /// </summary>
    /// <param name="testCase">The prediction geometry used to size the destination.</param>
    /// <param name="destinationStride">The padded destination-row stride.</param>
    /// <returns>The guarded destination storage.</returns>
    private static byte[] CreateByteDestination(PredictionCase testCase, int destinationStride)
        => Enumerable.Repeat(ByteDestinationSentinel, DestinationPrefix + (destinationStride * testCase.Height) + DestinationSuffix).ToArray();

    /// <summary>
    /// Creates a ushort destination whose prefix, row padding, and suffix expose stores outside the prediction block.
    /// </summary>
    /// <param name="testCase">The prediction geometry used to size the destination.</param>
    /// <param name="destinationStride">The padded destination-row stride.</param>
    /// <returns>The guarded destination storage.</returns>
    private static ushort[] CreateHighBitDepthDestination(PredictionCase testCase, int destinationStride)
        => Enumerable
            .Repeat(HighBitDepthDestinationSentinel, DestinationPrefix + (destinationStride * testCase.Height) + DestinationSuffix)
            .ToArray();

    /// <summary>
    /// Creates caller-owned two-dimensional intermediate storage using AV1's eight-tap vertical extent.
    /// </summary>
    /// <param name="testCase">The prediction geometry and fractional phases.</param>
    /// <returns>The required scratch storage, or an empty array for copy and one-dimensional predictions.</returns>
    private static short[] CreateScratch(PredictionCase testCase)
        => testCase.HorizontalPhase == 0 || testCase.VerticalPhase == 0
            ? []
            : new short[Math.Max(testCase.Width, 16) * (testCase.Height + FilterTapCount - 1)];

    /// <summary>
    /// Applies the reference decoder's single-reference copy or convolution equations to an 8-bit prediction block.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The source-row stride.</param>
    /// <param name="sourceOrigin">The integer-position sample index.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction filters, phases, and geometry.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void ApplyReference(
        byte[] source,
        int sourceStride,
        int sourceOrigin,
        byte[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        int bitDepth)
    {
        if (testCase.HorizontalPhase == 0 && testCase.VerticalPhase == 0)
        {
            for (int row = 0; row < testCase.Height; row++)
            {
                source.AsSpan(sourceOrigin + (row * sourceStride), testCase.Width)
                    .CopyTo(destination.AsSpan(destinationOrigin + (row * destinationStride), testCase.Width));
            }

            return;
        }

        if (testCase.VerticalPhase == 0)
        {
            ReadOnlySpan<short> horizontal = GetCoefficients(testCase.HorizontalFilter, testCase.HorizontalPhase, testCase.Width <= 4);
            int round0 = GetRound0Bits(bitDepth);

            for (int row = 0; row < testCase.Height; row++)
            {
                for (int column = 0; column < testCase.Width; column++)
                {
                    int sourceIndex = sourceOrigin + (row * sourceStride) + column - FilterCenterOffset;
                    int sum = Convolve(source, sourceIndex, 1, horizontal);
                    int value = RoundPowerOfTwo(RoundPowerOfTwo(sum, round0), FilterBits - round0);
                    destination[destinationOrigin + (row * destinationStride) + column] = (byte)Math.Clamp(value, 0, byte.MaxValue);
                }
            }

            return;
        }

        if (testCase.HorizontalPhase == 0)
        {
            ReadOnlySpan<short> vertical = GetCoefficients(testCase.VerticalFilter, testCase.VerticalPhase, testCase.Height <= 4);

            for (int row = 0; row < testCase.Height; row++)
            {
                for (int column = 0; column < testCase.Width; column++)
                {
                    int sourceIndex = sourceOrigin + ((row - FilterCenterOffset) * sourceStride) + column;
                    int sum = Convolve(source, sourceIndex, sourceStride, vertical);
                    int value = RoundPowerOfTwo(sum, FilterBits);
                    destination[destinationOrigin + (row * destinationStride) + column] = (byte)Math.Clamp(value, 0, byte.MaxValue);
                }
            }

            return;
        }

        ReadOnlySpan<short> horizontalCoefficients = GetCoefficients(testCase.HorizontalFilter, testCase.HorizontalPhase, testCase.Width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCoefficients(testCase.VerticalFilter, testCase.VerticalPhase, testCase.Height <= 4);

        ApplyTwoDimensionalReference(
            source, sourceStride, sourceOrigin, destination, destinationOrigin, destinationStride, testCase,
            horizontalCoefficients, verticalCoefficients, bitDepth);
    }

    /// <summary>
    /// Applies the reference decoder's single-reference copy or convolution equations to a high-bit-depth prediction block.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The source-row stride.</param>
    /// <param name="sourceOrigin">The integer-position sample index.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction filters, phases, and geometry.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void ApplyReference(
        ushort[] source,
        int sourceStride,
        int sourceOrigin,
        ushort[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        int bitDepth)
    {
        if (testCase.HorizontalPhase == 0 && testCase.VerticalPhase == 0)
        {
            for (int row = 0; row < testCase.Height; row++)
            {
                source.AsSpan(sourceOrigin + (row * sourceStride), testCase.Width)
                    .CopyTo(destination.AsSpan(destinationOrigin + (row * destinationStride), testCase.Width));
            }

            return;
        }

        int maximum = (1 << bitDepth) - 1;
        if (testCase.VerticalPhase == 0)
        {
            ReadOnlySpan<short> horizontal = GetCoefficients(testCase.HorizontalFilter, testCase.HorizontalPhase, testCase.Width <= 4);
            int round0 = GetRound0Bits(bitDepth);

            for (int row = 0; row < testCase.Height; row++)
            {
                for (int column = 0; column < testCase.Width; column++)
                {
                    int sourceIndex = sourceOrigin + (row * sourceStride) + column - FilterCenterOffset;
                    int sum = Convolve(source, sourceIndex, 1, horizontal);
                    int value = RoundPowerOfTwo(RoundPowerOfTwo(sum, round0), FilterBits - round0);
                    destination[destinationOrigin + (row * destinationStride) + column] = (ushort)Math.Clamp(value, 0, maximum);
                }
            }

            return;
        }

        if (testCase.HorizontalPhase == 0)
        {
            ReadOnlySpan<short> vertical = GetCoefficients(testCase.VerticalFilter, testCase.VerticalPhase, testCase.Height <= 4);

            for (int row = 0; row < testCase.Height; row++)
            {
                for (int column = 0; column < testCase.Width; column++)
                {
                    int sourceIndex = sourceOrigin + ((row - FilterCenterOffset) * sourceStride) + column;
                    int sum = Convolve(source, sourceIndex, sourceStride, vertical);
                    int value = RoundPowerOfTwo(sum, FilterBits);
                    destination[destinationOrigin + (row * destinationStride) + column] = (ushort)Math.Clamp(value, 0, maximum);
                }
            }

            return;
        }

        ReadOnlySpan<short> horizontalCoefficients = GetCoefficients(testCase.HorizontalFilter, testCase.HorizontalPhase, testCase.Width <= 4);
        ReadOnlySpan<short> verticalCoefficients = GetCoefficients(testCase.VerticalFilter, testCase.VerticalPhase, testCase.Height <= 4);

        ApplyTwoDimensionalReference(
            source, sourceStride, sourceOrigin, destination, destinationOrigin, destinationStride, testCase,
            horizontalCoefficients, verticalCoefficients, bitDepth);
    }

    /// <summary>
    /// Applies the reference decoder's biased two-pass 8-bit convolution and removes both intermediate bias terms after vertical filtering.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The source-row stride.</param>
    /// <param name="sourceOrigin">The integer-position sample index.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction geometry and phases.</param>
    /// <param name="horizontalCoefficients">The horizontal Q7 coefficient row.</param>
    /// <param name="verticalCoefficients">The vertical Q7 coefficient row.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void ApplyTwoDimensionalReference(
        byte[] source,
        int sourceStride,
        int sourceOrigin,
        byte[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        ReadOnlySpan<short> horizontalCoefficients,
        ReadOnlySpan<short> verticalCoefficients,
        int bitDepth)
    {
        short[] intermediate = new short[(testCase.Height + FilterTapCount - 1) * testCase.Width];
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int round0 = GetRound0Bits(bitDepth);

        for (int row = 0; row < testCase.Height + FilterTapCount - 1; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int sourceIndex = sourceOrigin + ((row - FilterCenterOffset) * sourceStride) + column - FilterCenterOffset;
                int sum = horizontalBias + Convolve(source, sourceIndex, 1, horizontalCoefficients);
                intermediate[(row * testCase.Width) + column] = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        WriteTwoDimensionalReference(intermediate, destination, destinationOrigin, destinationStride, testCase, verticalCoefficients, bitDepth);
    }

    /// <summary>
    /// Applies the reference decoder's biased two-pass high-bit-depth convolution and removes both intermediate bias terms after vertical filtering.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The source-row stride.</param>
    /// <param name="sourceOrigin">The integer-position sample index.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction geometry and phases.</param>
    /// <param name="horizontalCoefficients">The horizontal Q7 coefficient row.</param>
    /// <param name="verticalCoefficients">The vertical Q7 coefficient row.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void ApplyTwoDimensionalReference(
        ushort[] source,
        int sourceStride,
        int sourceOrigin,
        ushort[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        ReadOnlySpan<short> horizontalCoefficients,
        ReadOnlySpan<short> verticalCoefficients,
        int bitDepth)
    {
        short[] intermediate = new short[(testCase.Height + FilterTapCount - 1) * testCase.Width];
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int round0 = GetRound0Bits(bitDepth);

        for (int row = 0; row < testCase.Height + FilterTapCount - 1; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int sourceIndex = sourceOrigin + ((row - FilterCenterOffset) * sourceStride) + column - FilterCenterOffset;
                int sum = horizontalBias + Convolve(source, sourceIndex, 1, horizontalCoefficients);
                intermediate[(row * testCase.Width) + column] = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        WriteTwoDimensionalReference(intermediate, destination, destinationOrigin, destinationStride, testCase, verticalCoefficients, bitDepth);
    }

    /// <summary>
    /// Completes an 8-bit two-dimensional prediction from the independently generated biased intermediate block.
    /// </summary>
    /// <param name="intermediate">The horizontally filtered signed intermediate block.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction geometry.</param>
    /// <param name="verticalCoefficients">The vertical Q7 coefficient row.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void WriteTwoDimensionalReference(
        short[] intermediate,
        byte[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        ReadOnlySpan<short> verticalCoefficients,
        int bitDepth)
    {
        int round0 = GetRound0Bits(bitDepth);
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));

        for (int row = 0; row < testCase.Height; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int sum = verticalBias + Convolve(intermediate, (row * testCase.Width) + column, testCase.Width, verticalCoefficients);
                int value = RoundPowerOfTwo(sum, round1) - roundOffset;
                destination[destinationOrigin + (row * destinationStride) + column] = (byte)Math.Clamp(value, 0, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Completes a high-bit-depth two-dimensional prediction from the independently generated biased intermediate block.
    /// </summary>
    /// <param name="intermediate">The horizontally filtered signed intermediate block.</param>
    /// <param name="destination">The guarded destination storage.</param>
    /// <param name="destinationOrigin">The first active destination index.</param>
    /// <param name="destinationStride">The destination-row stride.</param>
    /// <param name="testCase">The prediction geometry.</param>
    /// <param name="verticalCoefficients">The vertical Q7 coefficient row.</param>
    /// <param name="bitDepth">The decoded sample precision.</param>
    private static void WriteTwoDimensionalReference(
        short[] intermediate,
        ushort[] destination,
        int destinationOrigin,
        int destinationStride,
        PredictionCase testCase,
        ReadOnlySpan<short> verticalCoefficients,
        int bitDepth)
    {
        int round0 = GetRound0Bits(bitDepth);
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));
        int maximum = (1 << bitDepth) - 1;

        for (int row = 0; row < testCase.Height; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int sum = verticalBias + Convolve(intermediate, (row * testCase.Width) + column, testCase.Width, verticalCoefficients);
                int value = RoundPowerOfTwo(sum, round1) - roundOffset;
                destination[destinationOrigin + (row * destinationStride) + column] = (ushort)Math.Clamp(value, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Computes one eight-tap Q7 convolution from 8-bit samples.
    /// </summary>
    /// <param name="source">The complete source storage.</param>
    /// <param name="sourceIndex">The first coefficient's source index.</param>
    /// <param name="sourceStep">The source-element distance between taps.</param>
    /// <param name="coefficients">The eight Q7 coefficients.</param>
    /// <returns>The unrounded convolution sum.</returns>
    private static int Convolve(byte[] source, int sourceIndex, int sourceStep, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + (tap * sourceStep)] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Computes one eight-tap Q7 convolution from high-bit-depth samples.
    /// </summary>
    /// <param name="source">The complete source storage.</param>
    /// <param name="sourceIndex">The first coefficient's source index.</param>
    /// <param name="sourceStep">The source-element distance between taps.</param>
    /// <param name="coefficients">The eight Q7 coefficients.</param>
    /// <returns>The unrounded convolution sum.</returns>
    private static int Convolve(ushort[] source, int sourceIndex, int sourceStep, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + (tap * sourceStep)] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Computes one eight-tap Q7 convolution from signed biased intermediate samples.
    /// </summary>
    /// <param name="source">The complete intermediate storage.</param>
    /// <param name="sourceIndex">The first coefficient's source index.</param>
    /// <param name="sourceStep">The source-element distance between taps.</param>
    /// <param name="coefficients">The eight Q7 coefficients.</param>
    /// <returns>The unrounded convolution sum.</returns>
    private static int Convolve(short[] source, int sourceIndex, int sourceStep, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + (tap * sourceStep)] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Selects one normative Q7 coefficient row independently of the production filter storage.
    /// </summary>
    /// <param name="filter">The interpolation-filter family.</param>
    /// <param name="phase">The one-sixteenth-sample phase.</param>
    /// <param name="useReducedFilter">A value indicating whether the dimension is four samples.</param>
    /// <returns>The eight-position Q7 coefficient row.</returns>
    private static ReadOnlySpan<short> GetCoefficients(Av1InterpolationFilter filter, int phase, bool useReducedFilter)
    {
        if (filter == Av1InterpolationFilter.Bilinear)
        {
            return phase switch
            {
                3 => BilinearPhase3,
                8 => BilinearPhase8,
                11 => BilinearPhase11,
                15 => BilinearPhase15,
                _ => throw new InvalidOperationException($"The test oracle has no bilinear coefficient row for phase {phase}.")
            };
        }

        if (useReducedFilter && filter == Av1InterpolationFilter.Sharp)
        {
            // AV1 maps sharp filtering on a four-sample dimension to the reduced regular table before convolution.
            filter = Av1InterpolationFilter.Regular;
        }

        return (filter, useReducedFilter, phase) switch
        {
            (Av1InterpolationFilter.Regular, false, 1) => RegularEightTapPhase1,
            (Av1InterpolationFilter.Smooth, false, 7) => SmoothEightTapPhase7,
            (Av1InterpolationFilter.Smooth, false, 15) => SmoothEightTapPhase15,
            (Av1InterpolationFilter.Sharp, false, 8) => SharpEightTapPhase8,
            (Av1InterpolationFilter.Regular, true, 3) => RegularFourTapPhase3,
            (Av1InterpolationFilter.Regular, true, 5) => RegularFourTapPhase5,
            (Av1InterpolationFilter.Regular, true, 13) => RegularFourTapPhase13,
            (Av1InterpolationFilter.Smooth, true, 7) => SmoothFourTapPhase7,
            (Av1InterpolationFilter.Smooth, true, 13) => SmoothFourTapPhase13,
            _ => throw new InvalidOperationException(
                $"The test oracle has no coefficient row for {filter}, phase {phase}, reduced {useReducedFilter}.")
        };
    }

    /// <summary>
    /// Gets the regular eight-tap Q7 kernel for phase 1 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> RegularEightTapPhase1 => [0, 2, -6, 126, 8, -2, 0, 0];

    /// <summary>
    /// Gets the smooth eight-tap Q7 kernel for phase 7 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> SmoothEightTapPhase7 => [0, -2, 16, 54, 48, 12, 0, 0];

    /// <summary>
    /// Gets the smooth eight-tap Q7 kernel for phase 15 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> SmoothEightTapPhase15 => [0, 0, 2, 34, 62, 28, 2, 0];

    /// <summary>
    /// Gets the sharp eight-tap Q7 kernel for phase 8 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> SharpEightTapPhase8 => [-4, 12, -24, 80, 80, -24, 12, -4];

    /// <summary>
    /// Gets the bilinear Q7 kernel for phase 3 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> BilinearPhase3 => [0, 0, 0, 104, 24, 0, 0, 0];

    /// <summary>
    /// Gets the bilinear Q7 kernel for phase 8 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> BilinearPhase8 => [0, 0, 0, 64, 64, 0, 0, 0];

    /// <summary>
    /// Gets the bilinear Q7 kernel for phase 11 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> BilinearPhase11 => [0, 0, 0, 40, 88, 0, 0, 0];

    /// <summary>
    /// Gets the bilinear Q7 kernel for phase 15 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> BilinearPhase15 => [0, 0, 0, 8, 120, 0, 0, 0];

    /// <summary>
    /// Gets the reduced regular Q7 kernel for phase 3 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> RegularFourTapPhase3 => [0, 0, -10, 116, 28, -6, 0, 0];

    /// <summary>
    /// Gets the reduced regular Q7 kernel for phase 5 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> RegularFourTapPhase5 => [0, 0, -12, 102, 48, -10, 0, 0];

    /// <summary>
    /// Gets the reduced regular Q7 kernel for phase 13 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> RegularFourTapPhase13 => [0, 0, -6, 28, 116, -10, 0, 0];

    /// <summary>
    /// Gets the reduced smooth Q7 kernel for phase 7 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> SmoothFourTapPhase7 => [0, 0, 14, 54, 48, 12, 0, 0];

    /// <summary>
    /// Gets the reduced smooth Q7 kernel for phase 13 from AOM's normative decoder table.
    /// </summary>
    private static ReadOnlySpan<short> SmoothFourTapPhase13 => [0, 0, 4, 40, 62, 22, 0, 0];

    /// <summary>
    /// Gets AOM's first convolution shift while keeping the biased intermediate within sixteen signed bits.
    /// </summary>
    /// <param name="bitDepth">The decoded sample precision.</param>
    /// <returns>The first convolution shift.</returns>
    private static int GetRound0Bits(int bitDepth)
    {
        int round0 = Round0Bits;
        int intermediateBitCount = bitDepth + FilterBits - round0 + 2;
        if (intermediateBitCount > 16)
        {
            round0 += intermediateBitCount - 16;
        }

        return round0;
    }

    /// <summary>
    /// Applies AOM's integer power-of-two rounding rule.
    /// </summary>
    /// <param name="value">The signed integer to divide.</param>
    /// <param name="bits">The base-2 divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    private static int RoundPowerOfTwo(int value, int bits) => (value + (1 << (bits - 1))) >> bits;

    /// <summary>
    /// Reports the first differing byte, including guarded padding, for one named prediction path.
    /// </summary>
    /// <param name="expected">The independently generated destination storage.</param>
    /// <param name="actual">The production destination storage.</param>
    /// <param name="testCase">The prediction scenario.</param>
    /// <param name="path">The production execution path.</param>
    private static void AssertEqual(byte[] expected, byte[] actual, PredictionCase testCase, string path)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                Assert.Fail($"{path} prediction '{testCase.Name}' differs at storage index {i}: expected {expected[i]}, actual {actual[i]}.");
            }
        }
    }

    /// <summary>
    /// Reports the first differing ushort, including guarded padding, for one named prediction path.
    /// </summary>
    /// <param name="expected">The independently generated destination storage.</param>
    /// <param name="actual">The production destination storage.</param>
    /// <param name="testCase">The prediction scenario.</param>
    /// <param name="path">The production execution path.</param>
    private static void AssertEqual(ushort[] expected, ushort[] actual, PredictionCase testCase, string path)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                Assert.Fail($"{path} prediction '{testCase.Name}' differs at storage index {i}: expected {expected[i]}, actual {actual[i]}.");
            }
        }
    }

    /// <summary>
    /// Describes one prediction path, filter pair, phase pair, and block geometry.
    /// </summary>
    private readonly struct PredictionCase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PredictionCase"/> struct.
        /// </summary>
        /// <param name="name">The diagnostic scenario name.</param>
        /// <param name="width">The active prediction width.</param>
        /// <param name="height">The active prediction height.</param>
        /// <param name="horizontalFilter">The horizontal interpolation-filter family.</param>
        /// <param name="verticalFilter">The vertical interpolation-filter family.</param>
        /// <param name="horizontalPhase">The horizontal one-sixteenth-sample phase.</param>
        /// <param name="verticalPhase">The vertical one-sixteenth-sample phase.</param>
        public PredictionCase(
            string name,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase)
        {
            this.Name = name;
            this.Width = width;
            this.Height = height;
            this.HorizontalFilter = horizontalFilter;
            this.VerticalFilter = verticalFilter;
            this.HorizontalPhase = horizontalPhase;
            this.VerticalPhase = verticalPhase;
        }

        /// <summary>
        /// Gets the diagnostic scenario name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the active prediction width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the active prediction height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the horizontal interpolation-filter family.
        /// </summary>
        public Av1InterpolationFilter HorizontalFilter { get; }

        /// <summary>
        /// Gets the vertical interpolation-filter family.
        /// </summary>
        public Av1InterpolationFilter VerticalFilter { get; }

        /// <summary>
        /// Gets the horizontal one-sixteenth-sample phase.
        /// </summary>
        public int HorizontalPhase { get; }

        /// <summary>
        /// Gets the vertical one-sixteenth-sample phase.
        /// </summary>
        public int VerticalPhase { get; }
    }
}
