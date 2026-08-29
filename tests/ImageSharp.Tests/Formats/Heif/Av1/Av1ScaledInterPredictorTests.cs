// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 reference scaling and variable-phase inter convolution against an independent libaom-shaped oracle.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ScaledInterPredictorTests
{
    /// <summary>
    /// The number of fractional bits in each interpolation coefficient.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The ordinary first-pass rounding distance.
    /// </summary>
    private const int Round0Bits = 3;

    /// <summary>
    /// The number of samples in every stored interpolation row.
    /// </summary>
    private const int FilterTapCount = 8;

    /// <summary>
    /// The source border retained around the independently generated active coordinates.
    /// </summary>
    private const int SourcePadding = 16;

    /// <summary>
    /// The guarded destination elements before the active block.
    /// </summary>
    private const int DestinationPrefix = 11;

    /// <summary>
    /// The guarded destination elements after each active row.
    /// </summary>
    private const int DestinationRowPadding = 9;

    /// <summary>
    /// The guarded destination elements after the final row.
    /// </summary>
    private const int DestinationSuffix = 17;

    /// <summary>
    /// The byte value used to detect writes outside the active destination block.
    /// </summary>
    private const byte ByteSentinel = 0xD3;

    /// <summary>
    /// The ushort value used to detect writes outside the active destination block.
    /// </summary>
    private const ushort UInt16Sentinel = 0xDEAD;

    /// <summary>
    /// Exercises the native vector path and the complete scalar fallback in separate processes.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the pinned Q14 scale factors, Q10 steps, and signed coordinate rounding.
    /// </summary>
    [Fact]
    public void ReferenceScaleMatchesPinnedLibaomFixedPointRules()
    {
        Av1ReferenceScale downscaledReference = new(40, 24, 64, 48);

        Assert.True(downscaledReference.IsScaled);
        Assert.Equal(10240, downscaledReference.HorizontalScale);
        Assert.Equal(8192, downscaledReference.VerticalScale);
        Assert.Equal(640, downscaledReference.HorizontalStep);
        Assert.Equal(512, downscaledReference.VerticalStep);
        Assert.Equal(ScaleCoordinate(37, 10240), downscaledReference.ScaleHorizontal(37));
        Assert.Equal(ScaleCoordinate(-37, 10240), downscaledReference.ScaleHorizontal(-37));

        Av1ReferenceScale enlargedReference = new(96, 72, 64, 48);

        Assert.Equal(24576, enlargedReference.HorizontalScale);
        Assert.Equal(24576, enlargedReference.VerticalScale);
        Assert.Equal(1536, enlargedReference.HorizontalStep);
        Assert.Equal(1536, enlargedReference.VerticalStep);

        Av1ReferenceScale identity = new(64, 48, 64, 48);

        Assert.False(identity.IsScaled);
        Assert.Equal(1024, identity.HorizontalStep);
        Assert.Equal(1024, identity.VerticalStep);
    }

    /// <summary>
    /// Verifies exact scaled 8-bit output, variable filter phases, vector tails, and untouched destination padding.
    /// </summary>
    [Fact]
    public void BytePredictionMatchesLibaomOracleAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateBytePredictions, PredictorConfigurations);

    /// <summary>
    /// Verifies exact scaled 8-, 10-, and 12-bit output under the native vector and scalar configurations.
    /// </summary>
    [Fact]
    public void HighBitDepthPredictionMatchesLibaomOracleAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthPredictions, PredictorConfigurations);

    /// <summary>
    /// Applies each scaled-prediction scenario to byte storage.
    /// </summary>
    private static void ValidateBytePredictions()
    {
        foreach (ScaledPredictionCase testCase in CreatePredictionCases())
        {
            byte[] source = CreateByteSource(testCase, out int sourceStride, out int sourceOrigin);
            int destinationStride = testCase.Width + DestinationRowPadding;
            byte[] expected = CreateByteDestination(testCase, destinationStride);
            byte[] actual = (byte[])expected.Clone();
            short[] scratch = new short[
                Av1ScaledInterPredictor.GetScaledScratchLength(
                    testCase.Width,
                    testCase.Height,
                    testCase.VerticalPhase,
                    testCase.VerticalStep)];

            ApplyReference(source, sourceStride, sourceOrigin, expected, destinationStride, testCase, 8);

            Av1ScaledInterPredictor.PredictScaled(
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
                testCase.HorizontalStep,
                testCase.VerticalPhase,
                testCase.VerticalStep,
                scratch);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Applies each scaled-prediction scenario to every supported high-bit-depth precision.
    /// </summary>
    private static void ValidateHighBitDepthPredictions()
    {
        int[] bitDepths = [8, 10, 12];
        foreach (int bitDepth in bitDepths)
        {
            foreach (ScaledPredictionCase testCase in CreatePredictionCases())
            {
                ushort[] source = CreateUInt16Source(testCase, bitDepth, out int sourceStride, out int sourceOrigin);
                int destinationStride = testCase.Width + DestinationRowPadding;
                ushort[] expected = CreateUInt16Destination(testCase, destinationStride);
                ushort[] actual = (ushort[])expected.Clone();
                short[] scratch = new short[
                    Av1ScaledInterPredictor.GetScaledScratchLength(
                        testCase.Width,
                        testCase.Height,
                        testCase.VerticalPhase,
                        testCase.VerticalStep)];

                ApplyReference(source, sourceStride, sourceOrigin, expected, destinationStride, testCase, bitDepth);

                Av1ScaledInterPredictor.PredictScaled(
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
                    testCase.HorizontalStep,
                    testCase.VerticalPhase,
                    testCase.VerticalStep,
                    bitDepth,
                    scratch);

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// Creates cases covering variable phases, every filter family, reduced kernels, and vector tails.
    /// </summary>
    private static ScaledPredictionCase[] CreatePredictionCases() =>
    [
        new("fixture-regular-8x8", 8, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 800, 512, 800, 512),
        new("fixture-regular-4x8", 4, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 800, 512, 800, 512),
        new("fixture-regular-8x4", 8, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, 800, 512, 800, 512),
        new("bilinear-variable-phase", 13, 9, Av1InterpolationFilter.Bilinear, Av1InterpolationFilter.Bilinear, 192, 1536, 512, 640),
        new("regular-smooth-wide", 20, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Smooth, 64, 2048, 448, 2048),
        new("regular-sharp-all-widths", 37, 7, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Sharp, 64, 2048, 512, 2048),
        new("sharp-bilinear-tail", 12, 5, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Bilinear, 512, 2048, 192, 2048),
        new("reduced-regular", 4, 8, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Smooth, 192, 2048, 448, 2048),
        new("reduced-sharp-maps-to-regular", 4, 8, Av1InterpolationFilter.Sharp, Av1InterpolationFilter.Smooth, 192, 2048, 448, 2048),
        new("reduced-smooth", 8, 4, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Smooth, 64, 2048, 832, 2048)
    ];

    /// <summary>
    /// Creates deterministic padded byte source storage for one prediction case.
    /// </summary>
    private static byte[] CreateByteSource(ScaledPredictionCase testCase, out int stride, out int origin)
    {
        GetSourceGeometry(testCase, out int width, out int height);
        stride = width;
        origin = (SourcePadding * stride) + SourcePadding;
        byte[] source = new byte[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                source[(row * stride) + column] = (byte)(((row * 29) + (column * 47) + (row * column * 3)) & byte.MaxValue);
            }
        }

        return source;
    }

    /// <summary>
    /// Creates deterministic padded ushort source storage for one prediction case.
    /// </summary>
    private static ushort[] CreateUInt16Source(ScaledPredictionCase testCase, int bitDepth, out int stride, out int origin)
    {
        GetSourceGeometry(testCase, out int width, out int height);
        stride = width;
        origin = (SourcePadding * stride) + SourcePadding;
        int maximum = (1 << bitDepth) - 1;
        ushort[] source = new ushort[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                source[(row * stride) + column] = (ushort)(((row * 269) + (column * 443) + (row * column * 31)) & maximum);
            }
        }

        return source;
    }

    /// <summary>
    /// Computes storage dimensions that keep every requested eight-tap read inside the test source.
    /// </summary>
    private static void GetSourceGeometry(ScaledPredictionCase testCase, out int width, out int height)
    {
        int maximumHorizontalPosition = testCase.HorizontalPhase + ((testCase.Width - 1) * testCase.HorizontalStep);
        int maximumVerticalPosition = testCase.VerticalPhase + ((testCase.Height - 1) * testCase.VerticalStep);
        width = (2 * SourcePadding) + (maximumHorizontalPosition >> Av1ReferenceScale.SubpixelBits) + FilterTapCount;
        height = (2 * SourcePadding) + (maximumVerticalPosition >> Av1ReferenceScale.SubpixelBits) + FilterTapCount;
    }

    /// <summary>
    /// Creates a guarded byte destination initialized to its sentinel.
    /// </summary>
    private static byte[] CreateByteDestination(ScaledPredictionCase testCase, int stride)
    {
        byte[] destination = new byte[DestinationPrefix + (stride * testCase.Height) + DestinationSuffix];
        Array.Fill(destination, ByteSentinel);
        return destination;
    }

    /// <summary>
    /// Creates a guarded ushort destination initialized to its sentinel.
    /// </summary>
    private static ushort[] CreateUInt16Destination(ScaledPredictionCase testCase, int stride)
    {
        ushort[] destination = new ushort[DestinationPrefix + (stride * testCase.Height) + DestinationSuffix];
        Array.Fill(destination, UInt16Sentinel);
        return destination;
    }

    /// <summary>
    /// Applies the independent variable-phase two-pass reference convolution to byte storage.
    /// </summary>
    private static void ApplyReference(
        byte[] source,
        int sourceStride,
        int sourceOrigin,
        byte[] destination,
        int destinationStride,
        ScaledPredictionCase testCase,
        int bitDepth)
    {
        short[] intermediate = CreateIntermediate(testCase);
        int intermediateStride = testCase.Width;
        int round0 = GetRound0Bits(bitDepth);
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        Span<short> coefficients = stackalloc short[FilterTapCount];

        for (int row = 0; row < intermediate.Length / intermediateStride; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int position = testCase.HorizontalPhase + (column * testCase.HorizontalStep);
                int sourceColumn = (position >> Av1ReferenceScale.SubpixelBits) - 3;
                FillCoefficients(testCase.HorizontalFilter, (position & Av1ReferenceScale.SubpixelMask) >> 6, testCase.Width <= 4, coefficients);
                int sourceIndex = sourceOrigin + ((row - 3) * sourceStride) + sourceColumn;
                int sum = horizontalBias + Convolve(source, sourceIndex, coefficients);
                intermediate[(row * intermediateStride) + column] = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        WriteReference(intermediate, intermediateStride, destination, destinationStride, testCase, bitDepth);
    }

    /// <summary>
    /// Applies the independent variable-phase two-pass reference convolution to ushort storage.
    /// </summary>
    private static void ApplyReference(
        ushort[] source,
        int sourceStride,
        int sourceOrigin,
        ushort[] destination,
        int destinationStride,
        ScaledPredictionCase testCase,
        int bitDepth)
    {
        short[] intermediate = CreateIntermediate(testCase);
        int intermediateStride = testCase.Width;
        int round0 = GetRound0Bits(bitDepth);
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        Span<short> coefficients = stackalloc short[FilterTapCount];

        for (int row = 0; row < intermediate.Length / intermediateStride; row++)
        {
            for (int column = 0; column < testCase.Width; column++)
            {
                int position = testCase.HorizontalPhase + (column * testCase.HorizontalStep);
                int sourceColumn = (position >> Av1ReferenceScale.SubpixelBits) - 3;
                FillCoefficients(testCase.HorizontalFilter, (position & Av1ReferenceScale.SubpixelMask) >> 6, testCase.Width <= 4, coefficients);
                int sourceIndex = sourceOrigin + ((row - 3) * sourceStride) + sourceColumn;
                int sum = horizontalBias + Convolve(source, sourceIndex, coefficients);
                intermediate[(row * intermediateStride) + column] = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        WriteReference(intermediate, intermediateStride, destination, destinationStride, testCase, bitDepth);
    }

    /// <summary>
    /// Allocates the oracle's independently shaped intermediate block.
    /// </summary>
    private static short[] CreateIntermediate(ScaledPredictionCase testCase)
    {
        int height = ((((testCase.Height - 1) * testCase.VerticalStep) + testCase.VerticalPhase) >> Av1ReferenceScale.SubpixelBits) + FilterTapCount;
        return new short[testCase.Width * height];
    }

    /// <summary>
    /// Completes byte output from the horizontally filtered intermediate block.
    /// </summary>
    private static void WriteReference(
        short[] intermediate,
        int intermediateStride,
        byte[] destination,
        int destinationStride,
        ScaledPredictionCase testCase,
        int bitDepth)
    {
        int maximum = byte.MaxValue;
        Span<short> coefficients = stackalloc short[FilterTapCount];
        for (int row = 0; row < testCase.Height; row++)
        {
            int position = testCase.VerticalPhase + (row * testCase.VerticalStep);
            int sourceRow = position >> Av1ReferenceScale.SubpixelBits;
            FillCoefficients(testCase.VerticalFilter, (position & Av1ReferenceScale.SubpixelMask) >> 6, testCase.Height <= 4, coefficients);
            for (int column = 0; column < testCase.Width; column++)
            {
                int value = FinishConvolution(intermediate, (sourceRow * intermediateStride) + column, intermediateStride, coefficients, bitDepth);
                destination[DestinationPrefix + (row * destinationStride) + column] = (byte)Math.Clamp(value, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Completes high-bit-depth output from the horizontally filtered intermediate block.
    /// </summary>
    private static void WriteReference(
        short[] intermediate,
        int intermediateStride,
        ushort[] destination,
        int destinationStride,
        ScaledPredictionCase testCase,
        int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        Span<short> coefficients = stackalloc short[FilterTapCount];
        for (int row = 0; row < testCase.Height; row++)
        {
            int position = testCase.VerticalPhase + (row * testCase.VerticalStep);
            int sourceRow = position >> Av1ReferenceScale.SubpixelBits;
            FillCoefficients(testCase.VerticalFilter, (position & Av1ReferenceScale.SubpixelMask) >> 6, testCase.Height <= 4, coefficients);
            for (int column = 0; column < testCase.Width; column++)
            {
                int value = FinishConvolution(intermediate, (sourceRow * intermediateStride) + column, intermediateStride, coefficients, bitDepth);
                destination[DestinationPrefix + (row * destinationStride) + column] = (ushort)Math.Clamp(value, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Removes both normative convolution biases after the vertical pass.
    /// </summary>
    private static int FinishConvolution(
        short[] intermediate,
        int sourceIndex,
        int sourceStride,
        ReadOnlySpan<short> coefficients,
        int bitDepth)
    {
        int round0 = GetRound0Bits(bitDepth);
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));
        int sum = verticalBias + Convolve(intermediate, sourceIndex, sourceStride, coefficients);
        return RoundPowerOfTwo(sum, round1) - roundOffset;
    }

    /// <summary>
    /// Computes one byte convolution sum.
    /// </summary>
    private static int Convolve(byte[] source, int sourceIndex, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + tap] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Computes one ushort convolution sum.
    /// </summary>
    private static int Convolve(ushort[] source, int sourceIndex, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + tap] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Computes one vertical convolution sum from the biased intermediate block.
    /// </summary>
    private static int Convolve(short[] source, int sourceIndex, int sourceStride, ReadOnlySpan<short> coefficients)
    {
        int sum = 0;
        for (int tap = 0; tap < FilterTapCount; tap++)
        {
            sum += source[sourceIndex + (tap * sourceStride)] * coefficients[tap];
        }

        return sum;
    }

    /// <summary>
    /// Selects one pinned coefficient row without reading production filter storage.
    /// </summary>
    private static void FillCoefficients(Av1InterpolationFilter filter, int phase, bool reduced, Span<short> destination)
    {
        destination.Clear();
        if (filter == Av1InterpolationFilter.Bilinear)
        {
            destination[3] = (short)(128 - (phase * 8));
            destination[4] = (short)(phase * 8);
            return;
        }

        if (reduced && filter == Av1InterpolationFilter.Sharp)
        {
            filter = Av1InterpolationFilter.Regular;
        }

        ReadOnlySpan<short> source = (filter, reduced, phase) switch
        {
            (Av1InterpolationFilter.Regular, false, 1) => [0, 2, -6, 126, 8, -2, 0, 0],
            (Av1InterpolationFilter.Regular, false, 4) => [0, 2, -14, 110, 38, -10, 2, 0],
            (Av1InterpolationFilter.Regular, false, 12) => [0, 2, -10, 38, 110, -14, 2, 0],
            (Av1InterpolationFilter.Smooth, false, 7) => [0, -2, 16, 54, 48, 12, 0, 0],
            (Av1InterpolationFilter.Sharp, false, 8) => [-4, 12, -24, 80, 80, -24, 12, -4],
            (Av1InterpolationFilter.Regular, true, 3) => [0, 0, -10, 116, 28, -6, 0, 0],
            (Av1InterpolationFilter.Regular, true, 4) => [0, 0, -12, 110, 38, -8, 0, 0],
            (Av1InterpolationFilter.Regular, true, 12) => [0, 0, -8, 38, 110, -12, 0, 0],
            (Av1InterpolationFilter.Smooth, true, 13) => [0, 0, 4, 40, 62, 22, 0, 0],
            _ => throw new InvalidOperationException($"The scaled oracle has no row for {filter}, phase {phase}, reduced {reduced}.")
        };

        source.CopyTo(destination);
    }

    /// <summary>
    /// Computes libaom's bit-depth-dependent first-pass shift.
    /// </summary>
    private static int GetRound0Bits(int bitDepth)
    {
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        return Round0Bits + Math.Max(intermediateRange - 16, 0);
    }

    /// <summary>
    /// Applies integer power-of-two rounding.
    /// </summary>
    private static int RoundPowerOfTwo(int value, int bits) => (value + (1 << (bits - 1))) >> bits;

    /// <summary>
    /// Independently applies libaom's signed Q14-to-Q10 scale conversion.
    /// </summary>
    private static int ScaleCoordinate(int value, int scale)
    {
        long scaled = ((long)value * scale) + ((scale - (1 << 14)) * 8L);
        const int shift = 8;
        const long rounding = 1L << (shift - 1);
        return scaled < 0
            ? (int)-((-scaled + rounding) >> shift)
            : (int)((scaled + rounding) >> shift);
    }

    /// <summary>
    /// Describes one scaled prediction case.
    /// </summary>
    private readonly struct ScaledPredictionCase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScaledPredictionCase"/> struct.
        /// </summary>
        public ScaledPredictionCase(
            string name,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep)
        {
            this.Name = name;
            this.Width = width;
            this.Height = height;
            this.HorizontalFilter = horizontalFilter;
            this.VerticalFilter = verticalFilter;
            this.HorizontalPhase = horizontalPhase;
            this.HorizontalStep = horizontalStep;
            this.VerticalPhase = verticalPhase;
            this.VerticalStep = verticalStep;
        }

        /// <summary>
        /// Gets the diagnostic case name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the output width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the output height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the horizontal interpolation filter.
        /// </summary>
        public Av1InterpolationFilter HorizontalFilter { get; }

        /// <summary>
        /// Gets the vertical interpolation filter.
        /// </summary>
        public Av1InterpolationFilter VerticalFilter { get; }

        /// <summary>
        /// Gets the initial horizontal Q10 position.
        /// </summary>
        public int HorizontalPhase { get; }

        /// <summary>
        /// Gets the horizontal Q10 source step.
        /// </summary>
        public int HorizontalStep { get; }

        /// <summary>
        /// Gets the initial vertical Q10 position.
        /// </summary>
        public int VerticalPhase { get; }

        /// <summary>
        /// Gets the vertical Q10 source step.
        /// </summary>
        public int VerticalStep { get; }

        /// <inheritdoc/>
        public override string ToString() => this.Name;
    }
}
