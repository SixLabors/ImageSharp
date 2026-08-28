// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs local and global affine warped-motion prediction blocks.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// The number of rows in one warped filter's horizontal intermediate tile.
    /// </summary>
    private const int WarpedIntermediateRows = 15;

    /// <summary>
    /// The number of columns in one warped filter tile.
    /// </summary>
    private const int WarpedTileSize = 8;

    /// <summary>
    /// The number of low model bits removed when addressing the warped filter table.
    /// </summary>
    private const int WarpedDifferencePrecisionBits = 10;

    /// <summary>
    /// The number of fractional positions in one warped pixel.
    /// </summary>
    private const int WarpedPixelPrecisionShifts = 64;

    /// <summary>
    /// Gets the number of signed 16-bit elements required by warped prediction.
    /// </summary>
    public const int WarpedScratchLength = WarpedIntermediateRows * WarpedTileSize;

    /// <summary>
    /// Supplies the horizontal and vertical eight-tap dot products for one execution width.
    /// </summary>
    private interface IWarpedConvolution
    {
        /// <summary>
        /// Convolves eight adjacent unsigned byte samples.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <returns>The sum of the eight sample-coefficient products.</returns>
        public static abstract int Convolve(ref byte source, ref short coefficients);

        /// <summary>
        /// Convolves eight adjacent unsigned high-bit-depth samples.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <returns>The sum of the eight sample-coefficient products.</returns>
        public static abstract int Convolve(ref ushort source, ref short coefficients);

        /// <summary>
        /// Convolves eight vertically strided unsigned intermediate samples.
        /// </summary>
        /// <param name="source">The first intermediate sample.</param>
        /// <param name="stride">The distance between intermediate rows.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <returns>The sum of the eight sample-coefficient products.</returns>
        public static abstract int ConvolveVertical(ref ushort source, int stride, ref short coefficients);
    }

    /// <summary>
    /// Reconstructs an 8-bit affine warped prediction using the widest supported convolution operator.
    /// </summary>
    public static void PredictWarped(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<byte> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            PredictWarped<WarpedVector128Convolution>(
                source,
                sourceStride,
                sourceOrigin,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                destinationPosition,
                width,
                height,
                subsamplingX,
                subsamplingY,
                parameters,
                scratch);

            return;
        }

        PredictWarped<WarpedScalarConvolution>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            parameters,
            scratch);
    }

    /// <summary>
    /// Reconstructs a high-bit-depth affine warped prediction using the widest supported convolution operator.
    /// </summary>
    public static void PredictWarped(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            PredictWarped<WarpedVector128Convolution>(
                source,
                sourceStride,
                sourceOrigin,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                destinationPosition,
                width,
                height,
                subsamplingX,
                subsamplingY,
                bitDepth,
                parameters,
                scratch);

            return;
        }

        PredictWarped<WarpedScalarConvolution>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            bitDepth,
            parameters,
            scratch);
    }

    /// <summary>
    /// Reconstructs an 8-bit affine warped prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictWarpedScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<byte> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarped<WarpedScalarConvolution>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            parameters,
            scratch);

    /// <summary>
    /// Reconstructs a high-bit-depth affine warped prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictWarpedScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarped<WarpedScalarConvolution>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            bitDepth,
            parameters,
            scratch);

    /// <summary>
    /// Reconstructs one 8-bit warped block through a closed convolution operator.
    /// </summary>
    private static void PredictWarped<TConvolution>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<byte> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        where TConvolution : struct, IWarpedConvolution
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];
        int horizontalBias = 1 << (8 + FilterBits - 1);
        int verticalBias = 1 << (8 + (2 * FilterBits) - Round0Bits);
        int verticalRound = (2 * FilterBits) - Round0Bits;

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                for (int row = -7; row < 8; row++)
                {
                    int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
                    int phase = phaseX + (parameters.Beta * (row + 4));
                    for (int column = -4; column < 4; column++)
                    {
                        int sourceX = integerX + column - 3;
                        int sourceIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X + sourceX;
                        ref short coefficients = ref GetWarpedFilterReference(phase);
                        int sum = horizontalBias + TConvolution.Convolve(ref Unsafe.Add(ref sourceBase, sourceIndex), ref coefficients);
                        intermediate[((row + 7) * WarpedTileSize) + column + 4] = (ushort)RoundPowerOfTwoScalar(sum, Round0Bits);
                        phase += parameters.Alpha;
                    }
                }

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    for (int column = 0; column < tileWidth; column++)
                    {
                        ref ushort intermediateSource = ref intermediate[(row * WarpedTileSize) + column];
                        ref short coefficients = ref GetWarpedFilterReference(phase);
                        int sum = verticalBias + TConvolution.ConvolveVertical(ref intermediateSource, WarpedTileSize, ref coefficients);
                        int value = RoundPowerOfTwoScalar(sum, verticalRound) - (1 << 7) - (1 << 8);
                        Unsafe.Add(ref destinationBase, destinationRowOffset + tileColumn - destinationPosition.X + column) =
                            (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

                        phase += parameters.Gamma;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reconstructs one high-bit-depth warped block through a closed convolution operator.
    /// </summary>
    private static void PredictWarped<TConvolution>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        where TConvolution : struct, IWarpedConvolution
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];

        // Twelve-bit prediction increases round0 by two so the biased horizontal intermediate remains representable
        // in sixteen bits. Reducing round1 by the same amount preserves the complete normative Q14 shift.
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);
        int verticalRound = (2 * FilterBits) - round0;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int verticalBias = 1 << (bitDepth + (2 * FilterBits) - round0);
        int maximum = (1 << bitDepth) - 1;

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                for (int row = -7; row < 8; row++)
                {
                    int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
                    int phase = phaseX + (parameters.Beta * (row + 4));
                    for (int column = -4; column < 4; column++)
                    {
                        int sourceX = integerX + column - 3;
                        int sourceIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X + sourceX;
                        ref short coefficients = ref GetWarpedFilterReference(phase);
                        int sum = horizontalBias + TConvolution.Convolve(ref Unsafe.Add(ref sourceBase, sourceIndex), ref coefficients);
                        intermediate[((row + 7) * WarpedTileSize) + column + 4] = (ushort)RoundPowerOfTwoScalar(sum, round0);
                        phase += parameters.Alpha;
                    }
                }

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    for (int column = 0; column < tileWidth; column++)
                    {
                        ref ushort intermediateSource = ref intermediate[(row * WarpedTileSize) + column];
                        ref short coefficients = ref GetWarpedFilterReference(phase);
                        int sum = verticalBias + TConvolution.ConvolveVertical(ref intermediateSource, WarpedTileSize, ref coefficients);
                        int value = RoundPowerOfTwoScalar(sum, verticalRound) - (1 << (bitDepth - 1)) - (1 << bitDepth);
                        Unsafe.Add(ref destinationBase, destinationRowOffset + tileColumn - destinationPosition.X + column) =
                            (ushort)Math.Clamp(value, 0, maximum);

                        phase += parameters.Gamma;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Projects the center of one 8x8 output tile and derives its integer source position and reduced phases.
    /// </summary>
    private static void DeriveWarpedTilePosition(
        Av1GlobalMotionParameters parameters,
        int tileColumn,
        int tileRow,
        int subsamplingX,
        int subsamplingY,
        out int integerX,
        out int integerY,
        out int phaseX,
        out int phaseY)
    {
        int sourceX = (tileColumn + 4) << subsamplingX;
        int sourceY = (tileRow + 4) << subsamplingY;
        long projectedX = ((long)parameters[2] * sourceX) + ((long)parameters[3] * sourceY) + parameters[0];
        long projectedY = ((long)parameters[4] * sourceX) + ((long)parameters[5] * sourceY) + parameters[1];
        long planeX = projectedX >> subsamplingX;
        long planeY = projectedY >> subsamplingY;
        integerX = (int)(planeX >> Av1GlobalMotionParameters.ModelPrecisionBits);
        integerY = (int)(planeY >> Av1GlobalMotionParameters.ModelPrecisionBits);
        phaseX = (int)planeX & (Av1GlobalMotionParameters.ModelScale - 1);
        phaseY = (int)planeY & (Av1GlobalMotionParameters.ModelScale - 1);
        phaseX += (-4 * parameters.Alpha) + (-4 * parameters.Beta);
        phaseY += (-4 * parameters.Gamma) + (-4 * parameters.Delta);

        // Shear parameters are quantized to 64-model-unit steps. Clearing the same low bits after the tile-center
        // projection keeps negative and positive phases on the exact filter-table grid used by the bitstream model.
        phaseX &= -1 << 6;
        phaseY &= -1 << 6;
    }

    /// <summary>
    /// Gets a reference to the first coefficient for one reduced warped-filter phase.
    /// </summary>
    private static ref short GetWarpedFilterReference(int phase)
    {
        int filterIndex = ((phase + (1 << (WarpedDifferencePrecisionBits - 1))) >> WarpedDifferencePrecisionBits) +
            WarpedPixelPrecisionShifts;

        return ref Unsafe.Add(ref MemoryMarshal.GetReference(WarpedFilter), filterIndex * FilterCoefficientCount);
    }

    /// <summary>
    /// Divides a nonnegative value by a power of two with nearest-integer rounding.
    /// </summary>
    private static int RoundPowerOfTwoScalar(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Executes warped dot products with portable 128-bit SIMD.
    /// </summary>
    private readonly struct WarpedVector128Convolution : IWarpedConvolution
    {
        /// <inheritdoc/>
        public static int Convolve(ref byte source, ref short coefficients)
        {
            Vector128<byte> packed = Vector128.LoadUnsafe(ref source);
            (Vector128<ushort> samples, _) = Vector128.Widen(packed);
            return MultiplyAndSum(samples, Vector128.LoadUnsafe(ref coefficients));
        }

        /// <inheritdoc/>
        public static int Convolve(ref ushort source, ref short coefficients)
            => MultiplyAndSum(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref coefficients));

        /// <inheritdoc/>
        public static int ConvolveVertical(ref ushort source, int stride, ref short coefficients)
        {
            Vector128<ushort> samples = Vector128.Create(
                source,
                Unsafe.Add(ref source, stride),
                Unsafe.Add(ref source, stride * 2),
                Unsafe.Add(ref source, stride * 3),
                Unsafe.Add(ref source, stride * 4),
                Unsafe.Add(ref source, stride * 5),
                Unsafe.Add(ref source, stride * 6),
                Unsafe.Add(ref source, stride * 7));

            return MultiplyAndSum(samples, Vector128.LoadUnsafe(ref coefficients));
        }

        /// <summary>
        /// Widens unsigned samples and signed coefficients before accumulating their exact 32-bit products.
        /// </summary>
        private static int MultiplyAndSum(Vector128<ushort> samples, Vector128<short> coefficients)
        {
            (Vector128<uint> sampleLower, Vector128<uint> sampleUpper) = Vector128.Widen(samples);
            (Vector128<int> coefficientLower, Vector128<int> coefficientUpper) = Vector128.Widen(coefficients);
            return Vector128.Sum(sampleLower.AsInt32() * coefficientLower) +
                Vector128.Sum(sampleUpper.AsInt32() * coefficientUpper);
        }
    }

    /// <summary>
    /// Executes warped dot products without explicit hardware intrinsics.
    /// </summary>
    private readonly struct WarpedScalarConvolution : IWarpedConvolution
    {
        /// <inheritdoc/>
        public static int Convolve(ref byte source, ref short coefficients)
        {
            int sum = 0;
            for (nuint index = 0; index < FilterCoefficientCount; index++)
            {
                sum += Unsafe.Add(ref source, index) * Unsafe.Add(ref coefficients, index);
            }

            return sum;
        }

        /// <inheritdoc/>
        public static int Convolve(ref ushort source, ref short coefficients)
        {
            int sum = 0;
            for (nuint index = 0; index < FilterCoefficientCount; index++)
            {
                sum += Unsafe.Add(ref source, index) * Unsafe.Add(ref coefficients, index);
            }

            return sum;
        }

        /// <inheritdoc/>
        public static int ConvolveVertical(ref ushort source, int stride, ref short coefficients)
        {
            int sum = 0;
            for (int index = 0; index < FilterCoefficientCount; index++)
            {
                sum += Unsafe.Add(ref source, index * stride) * Unsafe.Add(ref coefficients, index);
            }

            return sum;
        }
    }
}
