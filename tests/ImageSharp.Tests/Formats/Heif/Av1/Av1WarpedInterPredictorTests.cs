// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
    /// The hardware configurations covering every descending SIMD width and the scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies exact 8-bit prediction against the current libaom scalar equations.
    /// </summary>
    [Fact]
    public void BytePredictionMatchesCurrentLibaomOracleAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateBytePrediction, PredictorConfigurations);

    /// <summary>
    /// Verifies exact 8-, 10-, and 12-bit prediction against the current libaom scalar equations.
    /// </summary>
    [Fact]
    public void HighBitDepthPredictionMatchesCurrentLibaomOracleAcrossIntrinsicConfigurations()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHighBitDepthPrediction, PredictorConfigurations);

    /// <summary>
    /// Applies the current-libaom multi-sample affine model to deterministic byte storage.
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

        Av1GlobalMotionParameters parameters = CreateCurrentLibaomParameters();
        for (int subsampling = 0; subsampling <= 1; subsampling++)
        {
            byte[] expected = new byte[destinationStride * height];
            byte[] actual = new byte[destinationStride * height];
            Array.Fill(expected, (byte)0xD3);
            Array.Fill(actual, (byte)0xD3);
            short[] actualScratch = new short[Av1WarpedInterPredictor.WarpedScratchLength];
            Point destinationPosition = subsampling == 0 ? new Point(32, 24) : new Point(16, 12);

            PredictCurrentLibaomReference(
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
                8,
                parameters);

            Av1WarpedInterPredictor.PredictWarped(
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
    /// Applies the current-libaom multi-sample affine model to every supported high-bit-depth precision.
    /// </summary>
    private static void ValidateHighBitDepthPrediction()
    {
        const int activeSize = 96;
        const int padding = 24;
        const int sourceStride = activeSize + (2 * padding);
        const int width = 13;
        const int height = 11;
        const int destinationStride = width + 7;
        Av1GlobalMotionParameters parameters = CreateCurrentLibaomParameters();
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
                short[] actualScratch = new short[Av1WarpedInterPredictor.WarpedScratchLength];
                Point destinationPosition = subsampling == 0 ? new Point(32, 24) : new Point(16, 12);

                PredictCurrentLibaomReference(
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
                    parameters);

                Av1WarpedInterPredictor.PredictWarped(
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
    /// Reconstructs one warped block by directly transcribing current libaom's scalar affine loops.
    /// </summary>
    private static void PredictCurrentLibaomReference<TPixel>(
        ReadOnlySpan<TPixel> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<TPixel> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters)
        where TPixel : unmanaged, IBinaryInteger<TPixel>
    {
        const int filterBits = 7;
        const int filterTaps = 8;
        const int tileSize = 8;
        const int modelPrecisionBits = 16;
        const int phasePrecisionBits = 10;
        const int pixelPrecisionShifts = 64;
        Span<int> intermediate = stackalloc int[15 * tileSize];

        // Current libaom raises round0 by two for 12-bit sources so the biased horizontal intermediate
        // remains representable in 16 bits, then removes those two bits from the vertical rounding.
        int intermediateRange = bitDepth + filterBits - 3 + 2;
        int round0 = 3 + Math.Max(intermediateRange - 16, 0);
        int verticalRound = (2 * filterBits) - round0;
        int horizontalBias = 1 << (bitDepth + filterBits - 1);
        int verticalBias = 1 << (bitDepth + (2 * filterBits) - round0);
        int maximum = (1 << bitDepth) - 1;

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += tileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += tileSize)
            {
                int centerX = (tileColumn + 4) << subsamplingX;
                int centerY = (tileRow + 4) << subsamplingY;
                long projectedX =
                    ((long)parameters[2] * centerX) + ((long)parameters[3] * centerY) + parameters[0];

                long projectedY =
                    ((long)parameters[4] * centerX) + ((long)parameters[5] * centerY) + parameters[1];

                long planeX = projectedX >> subsamplingX;
                long planeY = projectedY >> subsamplingY;
                int integerX = (int)(planeX >> modelPrecisionBits);
                int integerY = (int)(planeY >> modelPrecisionBits);
                int phaseX = (int)planeX & ((1 << modelPrecisionBits) - 1);
                int phaseY = (int)planeY & ((1 << modelPrecisionBits) - 1);
                phaseX += (-4 * parameters.Alpha) + (-4 * parameters.Beta);
                phaseY += (-4 * parameters.Gamma) + (-4 * parameters.Delta);
                phaseX &= -1 << 6;
                phaseY &= -1 << 6;

                for (int row = -7; row < 8; row++)
                {
                    int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
                    int phase = phaseX + (parameters.Beta * (row + 4));
                    for (int column = -4; column < 4; column++)
                    {
                        int sourceX = integerX + column - 3;
                        int filterIndex = RoundPowerOfTwo(phase, phasePrecisionBits) + pixelPrecisionShifts;
                        int coefficientOffset = filterIndex * filterTaps;
                        int sum = horizontalBias;
                        for (int tap = 0; tap < filterTaps; tap++)
                        {
                            int sampleX = Math.Clamp(sourceX + tap, 0, sourceWidth - 1);
                            int sourceIndex =
                                ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X + sampleX;

                            int sample = int.CreateChecked(source[sourceIndex]);
                            sum += sample * CurrentLibaomWarpedFilter[coefficientOffset + tap];
                        }

                        intermediate[((row + 7) * tileSize) + column + 4] = RoundPowerOfTwo(sum, round0);
                        phase += parameters.Alpha;
                    }
                }

                int lastRow = Math.Min(4, destinationPosition.Y + height - tileRow - 4);
                for (int row = -4; row < lastRow; row++)
                {
                    int phase = phaseY + (parameters.Delta * (row + 4));
                    int lastColumn = Math.Min(4, destinationPosition.X + width - tileColumn - 4);
                    for (int column = -4; column < lastColumn; column++)
                    {
                        int filterIndex = RoundPowerOfTwo(phase, phasePrecisionBits) + pixelPrecisionShifts;
                        int coefficientOffset = filterIndex * filterTaps;
                        int sum = verticalBias;
                        for (int tap = 0; tap < filterTaps; tap++)
                        {
                            int intermediateIndex = ((row + tap + 4) * tileSize) + column + 4;
                            sum += intermediate[intermediateIndex] * CurrentLibaomWarpedFilter[coefficientOffset + tap];
                        }

                        int value = RoundPowerOfTwo(sum, verticalRound) - (1 << (bitDepth - 1)) - (1 << bitDepth);
                        int destinationIndex =
                            ((tileRow - destinationPosition.Y + row + 4) * destinationStride) +
                            tileColumn -
                            destinationPosition.X +
                            column +
                            4;

                        destination[destinationIndex] = TPixel.CreateChecked(Math.Clamp(value, 0, maximum));
                        phase += parameters.Gamma;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the 193 current-libaom eight-tap warped-filter phases used by the independent scalar oracle.
    /// </summary>
    private static ReadOnlySpan<short> CurrentLibaomWarpedFilter =>
    [
        0, 0, 127, 1, 0, 0, 0, 0,
        0, -1, 127, 2, 0, 0, 0, 0,
        1, -3, 127, 4, -1, 0, 0, 0,
        1, -4, 126, 6, -2, 1, 0, 0,
        1, -5, 126, 8, -3, 1, 0, 0,
        1, -6, 125, 11, -4, 1, 0, 0,
        1, -7, 124, 13, -4, 1, 0, 0,
        2, -8, 123, 15, -5, 1, 0, 0,
        2, -9, 122, 18, -6, 1, 0, 0,
        2, -10, 121, 20, -6, 1, 0, 0,
        2, -11, 120, 22, -7, 2, 0, 0,
        2, -12, 119, 25, -8, 2, 0, 0,
        3, -13, 117, 27, -8, 2, 0, 0,
        3, -13, 116, 29, -9, 2, 0, 0,
        3, -14, 114, 32, -10, 3, 0, 0,
        3, -15, 113, 35, -10, 2, 0, 0,
        3, -15, 111, 37, -11, 3, 0, 0,
        3, -16, 109, 40, -11, 3, 0, 0,
        3, -16, 108, 42, -12, 3, 0, 0,
        4, -17, 106, 45, -13, 3, 0, 0,
        4, -17, 104, 47, -13, 3, 0, 0,
        4, -17, 102, 50, -14, 3, 0, 0,
        4, -17, 100, 52, -14, 3, 0, 0,
        4, -18, 98, 55, -15, 4, 0, 0,
        4, -18, 96, 58, -15, 3, 0, 0,
        4, -18, 94, 60, -16, 4, 0, 0,
        4, -18, 91, 63, -16, 4, 0, 0,
        4, -18, 89, 65, -16, 4, 0, 0,
        4, -18, 87, 68, -17, 4, 0, 0,
        4, -18, 85, 70, -17, 4, 0, 0,
        4, -18, 82, 73, -17, 4, 0, 0,
        4, -18, 80, 75, -17, 4, 0, 0,
        4, -18, 78, 78, -18, 4, 0, 0,
        4, -17, 75, 80, -18, 4, 0, 0,
        4, -17, 73, 82, -18, 4, 0, 0,
        4, -17, 70, 85, -18, 4, 0, 0,
        4, -17, 68, 87, -18, 4, 0, 0,
        4, -16, 65, 89, -18, 4, 0, 0,
        4, -16, 63, 91, -18, 4, 0, 0,
        4, -16, 60, 94, -18, 4, 0, 0,
        3, -15, 58, 96, -18, 4, 0, 0,
        4, -15, 55, 98, -18, 4, 0, 0,
        3, -14, 52, 100, -17, 4, 0, 0,
        3, -14, 50, 102, -17, 4, 0, 0,
        3, -13, 47, 104, -17, 4, 0, 0,
        3, -13, 45, 106, -17, 4, 0, 0,
        3, -12, 42, 108, -16, 3, 0, 0,
        3, -11, 40, 109, -16, 3, 0, 0,
        3, -11, 37, 111, -15, 3, 0, 0,
        2, -10, 35, 113, -15, 3, 0, 0,
        3, -10, 32, 114, -14, 3, 0, 0,
        2, -9, 29, 116, -13, 3, 0, 0,
        2, -8, 27, 117, -13, 3, 0, 0,
        2, -8, 25, 119, -12, 2, 0, 0,
        2, -7, 22, 120, -11, 2, 0, 0,
        1, -6, 20, 121, -10, 2, 0, 0,
        1, -6, 18, 122, -9, 2, 0, 0,
        1, -5, 15, 123, -8, 2, 0, 0,
        1, -4, 13, 124, -7, 1, 0, 0,
        1, -4, 11, 125, -6, 1, 0, 0,
        1, -3, 8, 126, -5, 1, 0, 0,
        1, -2, 6, 126, -4, 1, 0, 0,
        0, -1, 4, 127, -3, 1, 0, 0,
        0, 0, 2, 127, -1, 0, 0, 0,
        0, 0, 0, 127, 1, 0, 0, 0,
        0, 0, -1, 127, 2, 0, 0, 0,
        0, 1, -3, 127, 4, -2, 1, 0,
        0, 1, -5, 127, 6, -2, 1, 0,
        0, 2, -6, 126, 8, -3, 1, 0,
        -1, 2, -7, 126, 11, -4, 2, -1,
        -1, 3, -8, 125, 13, -5, 2, -1,
        -1, 3, -10, 124, 16, -6, 3, -1,
        -1, 4, -11, 123, 18, -7, 3, -1,
        -1, 4, -12, 122, 20, -7, 3, -1,
        -1, 4, -13, 121, 23, -8, 3, -1,
        -2, 5, -14, 120, 25, -9, 4, -1,
        -1, 5, -15, 119, 27, -10, 4, -1,
        -1, 5, -16, 118, 30, -11, 4, -1,
        -2, 6, -17, 116, 33, -12, 5, -1,
        -2, 6, -17, 114, 35, -12, 5, -1,
        -2, 6, -18, 113, 38, -13, 5, -1,
        -2, 7, -19, 111, 41, -14, 6, -2,
        -2, 7, -19, 110, 43, -15, 6, -2,
        -2, 7, -20, 108, 46, -15, 6, -2,
        -2, 7, -20, 106, 49, -16, 6, -2,
        -2, 7, -21, 104, 51, -16, 7, -2,
        -2, 7, -21, 102, 54, -17, 7, -2,
        -2, 8, -21, 100, 56, -18, 7, -2,
        -2, 8, -22, 98, 59, -18, 7, -2,
        -2, 8, -22, 96, 62, -19, 7, -2,
        -2, 8, -22, 94, 64, -19, 7, -2,
        -2, 8, -22, 91, 67, -20, 8, -2,
        -2, 8, -22, 89, 69, -20, 8, -2,
        -2, 8, -22, 87, 72, -21, 8, -2,
        -2, 8, -21, 84, 74, -21, 8, -2,
        -2, 8, -22, 82, 77, -21, 8, -2,
        -2, 8, -21, 79, 79, -21, 8, -2,
        -2, 8, -21, 77, 82, -22, 8, -2,
        -2, 8, -21, 74, 84, -21, 8, -2,
        -2, 8, -21, 72, 87, -22, 8, -2,
        -2, 8, -20, 69, 89, -22, 8, -2,
        -2, 8, -20, 67, 91, -22, 8, -2,
        -2, 7, -19, 64, 94, -22, 8, -2,
        -2, 7, -19, 62, 96, -22, 8, -2,
        -2, 7, -18, 59, 98, -22, 8, -2,
        -2, 7, -18, 56, 100, -21, 8, -2,
        -2, 7, -17, 54, 102, -21, 7, -2,
        -2, 7, -16, 51, 104, -21, 7, -2,
        -2, 6, -16, 49, 106, -20, 7, -2,
        -2, 6, -15, 46, 108, -20, 7, -2,
        -2, 6, -15, 43, 110, -19, 7, -2,
        -2, 6, -14, 41, 111, -19, 7, -2,
        -1, 5, -13, 38, 113, -18, 6, -2,
        -1, 5, -12, 35, 114, -17, 6, -2,
        -1, 5, -12, 33, 116, -17, 6, -2,
        -1, 4, -11, 30, 118, -16, 5, -1,
        -1, 4, -10, 27, 119, -15, 5, -1,
        -1, 4, -9, 25, 120, -14, 5, -2,
        -1, 3, -8, 23, 121, -13, 4, -1,
        -1, 3, -7, 20, 122, -12, 4, -1,
        -1, 3, -7, 18, 123, -11, 4, -1,
        -1, 3, -6, 16, 124, -10, 3, -1,
        -1, 2, -5, 13, 125, -8, 3, -1,
        -1, 2, -4, 11, 126, -7, 2, -1,
        0, 1, -3, 8, 126, -6, 2, 0,
        0, 1, -2, 6, 127, -5, 1, 0,
        0, 1, -2, 4, 127, -3, 1, 0,
        0, 0, 0, 2, 127, -1, 0, 0,
        0, 0, 0, 1, 127, 0, 0, 0,
        0, 0, 0, -1, 127, 2, 0, 0,
        0, 0, 1, -3, 127, 4, -1, 0,
        0, 0, 1, -4, 126, 6, -2, 1,
        0, 0, 1, -5, 126, 8, -3, 1,
        0, 0, 1, -6, 125, 11, -4, 1,
        0, 0, 1, -7, 124, 13, -4, 1,
        0, 0, 2, -8, 123, 15, -5, 1,
        0, 0, 2, -9, 122, 18, -6, 1,
        0, 0, 2, -10, 121, 20, -6, 1,
        0, 0, 2, -11, 120, 22, -7, 2,
        0, 0, 2, -12, 119, 25, -8, 2,
        0, 0, 3, -13, 117, 27, -8, 2,
        0, 0, 3, -13, 116, 29, -9, 2,
        0, 0, 3, -14, 114, 32, -10, 3,
        0, 0, 3, -15, 113, 35, -10, 2,
        0, 0, 3, -15, 111, 37, -11, 3,
        0, 0, 3, -16, 109, 40, -11, 3,
        0, 0, 3, -16, 108, 42, -12, 3,
        0, 0, 4, -17, 106, 45, -13, 3,
        0, 0, 4, -17, 104, 47, -13, 3,
        0, 0, 4, -17, 102, 50, -14, 3,
        0, 0, 4, -17, 100, 52, -14, 3,
        0, 0, 4, -18, 98, 55, -15, 4,
        0, 0, 4, -18, 96, 58, -15, 3,
        0, 0, 4, -18, 94, 60, -16, 4,
        0, 0, 4, -18, 91, 63, -16, 4,
        0, 0, 4, -18, 89, 65, -16, 4,
        0, 0, 4, -18, 87, 68, -17, 4,
        0, 0, 4, -18, 85, 70, -17, 4,
        0, 0, 4, -18, 82, 73, -17, 4,
        0, 0, 4, -18, 80, 75, -17, 4,
        0, 0, 4, -18, 78, 78, -18, 4,
        0, 0, 4, -17, 75, 80, -18, 4,
        0, 0, 4, -17, 73, 82, -18, 4,
        0, 0, 4, -17, 70, 85, -18, 4,
        0, 0, 4, -17, 68, 87, -18, 4,
        0, 0, 4, -16, 65, 89, -18, 4,
        0, 0, 4, -16, 63, 91, -18, 4,
        0, 0, 4, -16, 60, 94, -18, 4,
        0, 0, 3, -15, 58, 96, -18, 4,
        0, 0, 4, -15, 55, 98, -18, 4,
        0, 0, 3, -14, 52, 100, -17, 4,
        0, 0, 3, -14, 50, 102, -17, 4,
        0, 0, 3, -13, 47, 104, -17, 4,
        0, 0, 3, -13, 45, 106, -17, 4,
        0, 0, 3, -12, 42, 108, -16, 3,
        0, 0, 3, -11, 40, 109, -16, 3,
        0, 0, 3, -11, 37, 111, -15, 3,
        0, 0, 2, -10, 35, 113, -15, 3,
        0, 0, 3, -10, 32, 114, -14, 3,
        0, 0, 2, -9, 29, 116, -13, 3,
        0, 0, 2, -8, 27, 117, -13, 3,
        0, 0, 2, -8, 25, 119, -12, 2,
        0, 0, 2, -7, 22, 120, -11, 2,
        0, 0, 1, -6, 20, 121, -10, 2,
        0, 0, 1, -6, 18, 122, -9, 2,
        0, 0, 1, -5, 15, 123, -8, 2,
        0, 0, 1, -4, 13, 124, -7, 1,
        0, 0, 1, -4, 11, 125, -6, 1,
        0, 0, 1, -3, 8, 126, -5, 1,
        0, 0, 1, -2, 6, 126, -4, 1,
        0, 0, 0, -1, 4, 127, -3, 1,
        0, 0, 0, 0, 2, 127, -1, 0,
        0, 0, 0, 0, 2, 127, -1, 0,
    ];

    /// <summary>
    /// Divides a signed fixed-point value by a power of two with current libaom's rounding.
    /// </summary>
    private static int RoundPowerOfTwo(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Creates one nontrivial affine model traced from the current-libaom-verified two-frame local-warp fixture.
    /// </summary>
    private static Av1GlobalMotionParameters CreateCurrentLibaomParameters()
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
