// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides recursive filter-intra traversal for the closed coefficient operators.
/// </content>
internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the coefficient set for one AV1 filter-intra prediction mode.
    /// </summary>
    internal interface IAv1FilterIntraPredictionOperator
    {
        /// <summary>
        /// Gets the filter-intra mode implemented by the operator.
        /// </summary>
        public static abstract Av1FilterIntraMode Mode { get; }

        /// <summary>
        /// Gets the eight seven-tap coefficient rows used by the operator.
        /// </summary>
        public static abstract ReadOnlySpan<sbyte> Taps { get; }
    }

    /// <summary>
    /// Applies one closed filter-intra coefficient operator using the widest useful SIMD width.
    /// </summary>
    /// <typeparam name="TOperator">The filter-intra coefficient set.</typeparam>
    /// <remarks>
    /// AV1 filter-intra predicts a two-row by four-column group from seven samples that may include previously
    /// predicted groups. The fixed-stride scratch surface preserves those dependencies with a one-sample top and left
    /// border. SIMD lanes hold the eight outputs of one group in row-major order; they do not span independent groups,
    /// because the next group can depend on the values just produced.
    /// </remarks>
    internal sealed class Av1FilterIntraPredictor<TOperator> : Av1FilterIntraPredictorBase
        where TOperator : struct, IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public override Av1FilterIntraMode Mode => TOperator.Mode;

        /// <inheritdoc/>
        public override void Predict(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height, Span<byte> scratch)
        {
            Span<byte> buffer = scratch[..ScratchLength];
            ref byte bufferBase = ref MemoryMarshal.GetReference(buffer);
            ref byte aboveBase = ref MemoryMarshal.GetReference(above);
            ref byte leftBase = ref MemoryMarshal.GetReference(left);
            ref sbyte taps = ref MemoryMarshal.GetReference(TOperator.Taps);
            Initialize(buffer, above, left, width, height, Unsafe.Subtract(ref aboveBase, 1));

            if (Vector256.IsHardwareAccelerated)
            {
                // Each tap vector contains the coefficient at one tap position for the eight row-major outputs in a
                // 2-by-4 group. Broadcasting the seven reconstructed inputs therefore evaluates all outputs together.
                Vector256<int> tap0 = CreateTapVector256(ref taps, 0);
                Vector256<int> tap1 = CreateTapVector256(ref taps, 1);
                Vector256<int> tap2 = CreateTapVector256(ref taps, 2);
                Vector256<int> tap3 = CreateTapVector256(ref taps, 3);
                Vector256<int> tap4 = CreateTapVector256(ref taps, 4);
                Vector256<int> tap5 = CreateTapVector256(ref taps, 5);
                Vector256<int> tap6 = CreateTapVector256(ref taps, 6);
                Vector256<int> rounding = Vector256.Create(8);
                Vector256<int> maximum = Vector256.Create(255);

                for (int row = 1; row <= height; row += 2)
                {
                    for (int column = 1; column <= width; column += 4)
                    {
                        int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                        Vector256<int> prediction = Calculate(
                            tap0,
                            tap1,
                            tap2,
                            tap3,
                            tap4,
                            tap5,
                            tap6,
                            Unsafe.Add(ref bufferBase, sourceOffset),
                            Unsafe.Add(ref bufferBase, sourceOffset + 1),
                            Unsafe.Add(ref bufferBase, sourceOffset + 2),
                            Unsafe.Add(ref bufferBase, sourceOffset + 3),
                            Unsafe.Add(ref bufferBase, sourceOffset + 4),
                            Unsafe.Add(ref bufferBase, sourceOffset + BufferStride),
                            Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride)));

                        prediction = Vector256.Clamp((prediction + rounding) >> 4, Vector256<int>.Zero, maximum);
                        StoreEightBytes(prediction, ref bufferBase, (row * BufferStride) + column, ((row + 1) * BufferStride) + column);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                // A 128-bit vector covers one four-sample output row. Low and high coefficient vectors describe the
                // first and second rows respectively while sharing the same seven reconstructed input broadcasts.
                Vector128<int> tap0Low = CreateTapVector128(ref taps, 0, 0);
                Vector128<int> tap1Low = CreateTapVector128(ref taps, 1, 0);
                Vector128<int> tap2Low = CreateTapVector128(ref taps, 2, 0);
                Vector128<int> tap3Low = CreateTapVector128(ref taps, 3, 0);
                Vector128<int> tap4Low = CreateTapVector128(ref taps, 4, 0);
                Vector128<int> tap5Low = CreateTapVector128(ref taps, 5, 0);
                Vector128<int> tap6Low = CreateTapVector128(ref taps, 6, 0);
                Vector128<int> tap0High = CreateTapVector128(ref taps, 0, 4);
                Vector128<int> tap1High = CreateTapVector128(ref taps, 1, 4);
                Vector128<int> tap2High = CreateTapVector128(ref taps, 2, 4);
                Vector128<int> tap3High = CreateTapVector128(ref taps, 3, 4);
                Vector128<int> tap4High = CreateTapVector128(ref taps, 4, 4);
                Vector128<int> tap5High = CreateTapVector128(ref taps, 5, 4);
                Vector128<int> tap6High = CreateTapVector128(ref taps, 6, 4);
                Vector128<int> rounding = Vector128.Create(8);
                Vector128<int> maximum = Vector128.Create(255);

                for (int row = 1; row <= height; row += 2)
                {
                    for (int column = 1; column <= width; column += 4)
                    {
                        int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                        int p0 = Unsafe.Add(ref bufferBase, sourceOffset);
                        int p1 = Unsafe.Add(ref bufferBase, sourceOffset + 1);
                        int p2 = Unsafe.Add(ref bufferBase, sourceOffset + 2);
                        int p3 = Unsafe.Add(ref bufferBase, sourceOffset + 3);
                        int p4 = Unsafe.Add(ref bufferBase, sourceOffset + 4);
                        int p5 = Unsafe.Add(ref bufferBase, sourceOffset + BufferStride);
                        int p6 = Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride));
                        Vector128<int> low = Calculate(tap0Low, tap1Low, tap2Low, tap3Low, tap4Low, tap5Low, tap6Low, p0, p1, p2, p3, p4, p5, p6);
                        Vector128<int> high = Calculate(tap0High, tap1High, tap2High, tap3High, tap4High, tap5High, tap6High, p0, p1, p2, p3, p4, p5, p6);
                        low = Vector128.Clamp((low + rounding) >> 4, Vector128<int>.Zero, maximum);
                        high = Vector128.Clamp((high + rounding) >> 4, Vector128<int>.Zero, maximum);
                        StoreFourBytes(low, ref bufferBase, (row * BufferStride) + column);
                        StoreFourBytes(high, ref bufferBase, ((row + 1) * BufferStride) + column);
                    }
                }
            }
            else
            {
                PredictGroupsScalar(buffer, width, height, 255, ref taps);
            }

            CopyToDestination(buffer, destination, destinationStride, width, height);
        }

        /// <inheritdoc/>
        public override void Predict(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth, Span<short> scratch)
        {
            Span<short> buffer = scratch[..ScratchLength];
            ref short bufferBase = ref MemoryMarshal.GetReference(buffer);
            ref short aboveBase = ref MemoryMarshal.GetReference(above);
            ref sbyte taps = ref MemoryMarshal.GetReference(TOperator.Taps);
            Initialize(buffer, above, left, width, height, Unsafe.Subtract(ref aboveBase, 1));
            int maximum = (1 << bitDepth) - 1;

            if (Vector256.IsHardwareAccelerated)
            {
                // High-bit-depth storage changes only the final clamp and narrowing. The Int32 accumulator layout is
                // identical to the eight-bit path, preserving all signed coefficient products before Q4 rounding.
                Vector256<int> tap0 = CreateTapVector256(ref taps, 0);
                Vector256<int> tap1 = CreateTapVector256(ref taps, 1);
                Vector256<int> tap2 = CreateTapVector256(ref taps, 2);
                Vector256<int> tap3 = CreateTapVector256(ref taps, 3);
                Vector256<int> tap4 = CreateTapVector256(ref taps, 4);
                Vector256<int> tap5 = CreateTapVector256(ref taps, 5);
                Vector256<int> tap6 = CreateTapVector256(ref taps, 6);
                Vector256<int> rounding = Vector256.Create(8);
                Vector256<int> maximumVector = Vector256.Create(maximum);

                for (int row = 1; row <= height; row += 2)
                {
                    for (int column = 1; column <= width; column += 4)
                    {
                        int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                        Vector256<int> prediction = Calculate(
                            tap0,
                            tap1,
                            tap2,
                            tap3,
                            tap4,
                            tap5,
                            tap6,
                            Unsafe.Add(ref bufferBase, sourceOffset),
                            Unsafe.Add(ref bufferBase, sourceOffset + 1),
                            Unsafe.Add(ref bufferBase, sourceOffset + 2),
                            Unsafe.Add(ref bufferBase, sourceOffset + 3),
                            Unsafe.Add(ref bufferBase, sourceOffset + 4),
                            Unsafe.Add(ref bufferBase, sourceOffset + BufferStride),
                            Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride)));

                        prediction = Vector256.Clamp((prediction + rounding) >> 4, Vector256<int>.Zero, maximumVector);
                        StoreEightShorts(prediction, ref bufferBase, (row * BufferStride) + column, ((row + 1) * BufferStride) + column);
                    }
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> tap0Low = CreateTapVector128(ref taps, 0, 0);
                Vector128<int> tap1Low = CreateTapVector128(ref taps, 1, 0);
                Vector128<int> tap2Low = CreateTapVector128(ref taps, 2, 0);
                Vector128<int> tap3Low = CreateTapVector128(ref taps, 3, 0);
                Vector128<int> tap4Low = CreateTapVector128(ref taps, 4, 0);
                Vector128<int> tap5Low = CreateTapVector128(ref taps, 5, 0);
                Vector128<int> tap6Low = CreateTapVector128(ref taps, 6, 0);
                Vector128<int> tap0High = CreateTapVector128(ref taps, 0, 4);
                Vector128<int> tap1High = CreateTapVector128(ref taps, 1, 4);
                Vector128<int> tap2High = CreateTapVector128(ref taps, 2, 4);
                Vector128<int> tap3High = CreateTapVector128(ref taps, 3, 4);
                Vector128<int> tap4High = CreateTapVector128(ref taps, 4, 4);
                Vector128<int> tap5High = CreateTapVector128(ref taps, 5, 4);
                Vector128<int> tap6High = CreateTapVector128(ref taps, 6, 4);
                Vector128<int> rounding = Vector128.Create(8);
                Vector128<int> maximumVector = Vector128.Create(maximum);

                for (int row = 1; row <= height; row += 2)
                {
                    for (int column = 1; column <= width; column += 4)
                    {
                        int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                        int p0 = Unsafe.Add(ref bufferBase, sourceOffset);
                        int p1 = Unsafe.Add(ref bufferBase, sourceOffset + 1);
                        int p2 = Unsafe.Add(ref bufferBase, sourceOffset + 2);
                        int p3 = Unsafe.Add(ref bufferBase, sourceOffset + 3);
                        int p4 = Unsafe.Add(ref bufferBase, sourceOffset + 4);
                        int p5 = Unsafe.Add(ref bufferBase, sourceOffset + BufferStride);
                        int p6 = Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride));
                        Vector128<int> low = Calculate(tap0Low, tap1Low, tap2Low, tap3Low, tap4Low, tap5Low, tap6Low, p0, p1, p2, p3, p4, p5, p6);
                        Vector128<int> high = Calculate(tap0High, tap1High, tap2High, tap3High, tap4High, tap5High, tap6High, p0, p1, p2, p3, p4, p5, p6);
                        low = Vector128.Clamp((low + rounding) >> 4, Vector128<int>.Zero, maximumVector);
                        high = Vector128.Clamp((high + rounding) >> 4, Vector128<int>.Zero, maximumVector);
                        StoreFourShorts(low, ref bufferBase, (row * BufferStride) + column);
                        StoreFourShorts(high, ref bufferBase, ((row + 1) * BufferStride) + column);
                    }
                }
            }
            else
            {
                PredictGroupsScalar(buffer, width, height, maximum, ref taps);
            }

            CopyToDestination(buffer, destination, destinationStride, width, height);
        }

        /// <inheritdoc/>
        public override void PredictScalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height, Span<byte> scratch)
        {
            Span<byte> buffer = scratch[..ScratchLength];
            ref byte aboveBase = ref MemoryMarshal.GetReference(above);
            ref sbyte taps = ref MemoryMarshal.GetReference(TOperator.Taps);
            Initialize(buffer, above, left, width, height, Unsafe.Subtract(ref aboveBase, 1));
            PredictGroupsScalar(buffer, width, height, 255, ref taps);
            CopyToDestinationScalar(buffer, destination, destinationStride, width, height);
        }

        /// <inheritdoc/>
        public override void PredictScalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth, Span<short> scratch)
        {
            Span<short> buffer = scratch[..ScratchLength];
            ref short aboveBase = ref MemoryMarshal.GetReference(above);
            ref sbyte taps = ref MemoryMarshal.GetReference(TOperator.Taps);
            Initialize(buffer, above, left, width, height, Unsafe.Subtract(ref aboveBase, 1));
            PredictGroupsScalar(buffer, width, height, (1 << bitDepth) - 1, ref taps);
            CopyToDestinationScalar(buffer, destination, destinationStride, width, height);
        }

        /// <summary>
        /// Initializes an 8-bit recursive filter-intra workspace from the prepared top and left references.
        /// </summary>
        /// <param name="buffer">The recursive workspace.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="topLeft">The shared top-left reference.</param>
        private static void Initialize(Span<byte> buffer, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height, byte topLeft)
        {
            // Predictions use one-based coordinates in the workspace. Row zero and column zero retain the prepared
            // references while later groups overwrite only the interior values on which following groups depend.
            buffer[0] = topLeft;
            above[..width].CopyTo(buffer[1..]);
            for (int row = 0; row < height; row++)
            {
                buffer[(row + 1) * BufferStride] = left[row];
            }
        }

        /// <summary>
        /// Initializes a high-bit-depth recursive filter-intra workspace from the prepared top and left references.
        /// </summary>
        /// <param name="buffer">The recursive workspace.</param>
        /// <param name="above">The top reference.</param>
        /// <param name="left">The left reference.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="topLeft">The shared top-left reference.</param>
        private static void Initialize(Span<short> buffer, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, short topLeft)
        {
            // Match the eight-bit one-based workspace so the recursive source offsets remain representation-agnostic.
            buffer[0] = topLeft;
            above[..width].CopyTo(buffer[1..]);
            for (int row = 0; row < height; row++)
            {
                buffer[(row + 1) * BufferStride] = left[row];
            }
        }

        /// <summary>
        /// Creates the coefficients for eight output samples at one tap position.
        /// </summary>
        /// <param name="taps">The first coefficient in the operator table.</param>
        /// <param name="tap">The tap position.</param>
        /// <returns>The coefficient vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> CreateTapVector256(ref sbyte taps, int tap)
            => Vector256.Create(
                (int)Unsafe.Add(ref taps, tap),
                Unsafe.Add(ref taps, 7 + tap),
                Unsafe.Add(ref taps, 14 + tap),
                Unsafe.Add(ref taps, 21 + tap),
                Unsafe.Add(ref taps, 28 + tap),
                Unsafe.Add(ref taps, 35 + tap),
                Unsafe.Add(ref taps, 42 + tap),
                Unsafe.Add(ref taps, 49 + tap));

        /// <summary>
        /// Creates the coefficients for four output samples at one tap position.
        /// </summary>
        /// <param name="taps">The first coefficient in the operator table.</param>
        /// <param name="tap">The tap position.</param>
        /// <param name="pixel">The first output sample.</param>
        /// <returns>The coefficient vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> CreateTapVector128(ref sbyte taps, int tap, int pixel)
            => Vector128.Create(
                (int)Unsafe.Add(ref taps, (pixel * 7) + tap),
                Unsafe.Add(ref taps, ((pixel + 1) * 7) + tap),
                Unsafe.Add(ref taps, ((pixel + 2) * 7) + tap),
                Unsafe.Add(ref taps, ((pixel + 3) * 7) + tap));

        /// <summary>
        /// Calculates eight filtered predictions from seven reconstructed samples.
        /// </summary>
        /// <param name="tap0">The first tap coefficients.</param>
        /// <param name="tap1">The second tap coefficients.</param>
        /// <param name="tap2">The third tap coefficients.</param>
        /// <param name="tap3">The fourth tap coefficients.</param>
        /// <param name="tap4">The fifth tap coefficients.</param>
        /// <param name="tap5">The sixth tap coefficients.</param>
        /// <param name="tap6">The seventh tap coefficients.</param>
        /// <param name="p0">The first reconstructed sample.</param>
        /// <param name="p1">The second reconstructed sample.</param>
        /// <param name="p2">The third reconstructed sample.</param>
        /// <param name="p3">The fourth reconstructed sample.</param>
        /// <param name="p4">The fifth reconstructed sample.</param>
        /// <param name="p5">The sixth reconstructed sample.</param>
        /// <param name="p6">The seventh reconstructed sample.</param>
        /// <returns>The unrounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Calculate(
            Vector256<int> tap0,
            Vector256<int> tap1,
            Vector256<int> tap2,
            Vector256<int> tap3,
            Vector256<int> tap4,
            Vector256<int> tap5,
            Vector256<int> tap6,
            int p0,
            int p1,
            int p2,
            int p3,
            int p4,
            int p5,
            int p6)
            => (tap0 * p0) + (tap1 * p1) + (tap2 * p2) + (tap3 * p3) + (tap4 * p4) + (tap5 * p5) + (tap6 * p6);

        /// <summary>
        /// Calculates four filtered predictions from seven reconstructed samples.
        /// </summary>
        /// <param name="tap0">The first tap coefficients.</param>
        /// <param name="tap1">The second tap coefficients.</param>
        /// <param name="tap2">The third tap coefficients.</param>
        /// <param name="tap3">The fourth tap coefficients.</param>
        /// <param name="tap4">The fifth tap coefficients.</param>
        /// <param name="tap5">The sixth tap coefficients.</param>
        /// <param name="tap6">The seventh tap coefficients.</param>
        /// <param name="p0">The first reconstructed sample.</param>
        /// <param name="p1">The second reconstructed sample.</param>
        /// <param name="p2">The third reconstructed sample.</param>
        /// <param name="p3">The fourth reconstructed sample.</param>
        /// <param name="p4">The fifth reconstructed sample.</param>
        /// <param name="p5">The sixth reconstructed sample.</param>
        /// <param name="p6">The seventh reconstructed sample.</param>
        /// <returns>The unrounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Calculate(
            Vector128<int> tap0,
            Vector128<int> tap1,
            Vector128<int> tap2,
            Vector128<int> tap3,
            Vector128<int> tap4,
            Vector128<int> tap5,
            Vector128<int> tap6,
            int p0,
            int p1,
            int p2,
            int p3,
            int p4,
            int p5,
            int p6)
            => (tap0 * p0) + (tap1 * p1) + (tap2 * p2) + (tap3 * p3) + (tap4 * p4) + (tap5 * p5) + (tap6 * p6);

        /// <summary>
        /// Stores eight 8-bit predictions into the two recursive output rows.
        /// </summary>
        /// <param name="prediction">The clamped predictions.</param>
        /// <param name="buffer">The recursive workspace origin.</param>
        /// <param name="firstRowOffset">The first output-row offset.</param>
        /// <param name="secondRowOffset">The second output-row offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreEightBytes(Vector256<int> prediction, ref byte buffer, int firstRowOffset, int secondRowOffset)
        {
            Vector256<ushort> narrowed16 = Vector256.Narrow(prediction.AsUInt32(), Vector256<uint>.Zero);
            Vector256<byte> narrowed8 = Vector256.Narrow(narrowed16, Vector256<ushort>.Zero);
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref buffer, firstRowOffset)) = narrowed8.AsUInt32().GetElement(0);
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref buffer, secondRowOffset)) = narrowed8.AsUInt32().GetElement(1);
        }

        /// <summary>
        /// Stores four 8-bit predictions into one recursive output row.
        /// </summary>
        /// <param name="prediction">The clamped predictions.</param>
        /// <param name="buffer">The recursive workspace origin.</param>
        /// <param name="offset">The output-row offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourBytes(Vector128<int> prediction, ref byte buffer, int offset)
        {
            Vector128<ushort> narrowed16 = Vector128.Narrow(prediction.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> narrowed8 = Vector128.Narrow(narrowed16, Vector128<ushort>.Zero);
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref buffer, offset)) = narrowed8.AsUInt32().GetElement(0);
        }

        /// <summary>
        /// Stores eight high-bit-depth predictions into the two recursive output rows.
        /// </summary>
        /// <param name="prediction">The clamped predictions.</param>
        /// <param name="buffer">The recursive workspace origin.</param>
        /// <param name="firstRowOffset">The first output-row offset.</param>
        /// <param name="secondRowOffset">The second output-row offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreEightShorts(Vector256<int> prediction, ref short buffer, int firstRowOffset, int secondRowOffset)
        {
            Vector256<short> narrowed = Vector256.Narrow(prediction, Vector256<int>.Zero);
            Unsafe.As<short, ulong>(ref Unsafe.Add(ref buffer, firstRowOffset)) = narrowed.AsUInt64().ToScalar();
            Unsafe.As<short, ulong>(ref Unsafe.Add(ref buffer, secondRowOffset)) = narrowed.AsUInt64().GetElement(1);
        }

        /// <summary>
        /// Stores four high-bit-depth predictions into one recursive output row.
        /// </summary>
        /// <param name="prediction">The clamped predictions.</param>
        /// <param name="buffer">The recursive workspace origin.</param>
        /// <param name="offset">The output-row offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourShorts(Vector128<int> prediction, ref short buffer, int offset)
        {
            Vector128<short> narrowed = Vector128.Narrow(prediction, Vector128<int>.Zero);
            Unsafe.As<short, ulong>(ref Unsafe.Add(ref buffer, offset)) = narrowed.AsUInt64().ToScalar();
        }

        /// <summary>
        /// Calculates every 8-bit recursive filter group without hardware intrinsics.
        /// </summary>
        /// <param name="buffer">The initialized recursive workspace.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="maximum">The maximum reconstructed sample.</param>
        /// <param name="taps">The first filter coefficient.</param>
        private static void PredictGroupsScalar(Span<byte> buffer, int width, int height, int maximum, ref sbyte taps)
        {
            ref byte bufferBase = ref MemoryMarshal.GetReference(buffer);
            for (int row = 1; row <= height; row += 2)
            {
                for (int column = 1; column <= width; column += 4)
                {
                    int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                    int p0 = Unsafe.Add(ref bufferBase, sourceOffset);
                    int p1 = Unsafe.Add(ref bufferBase, sourceOffset + 1);
                    int p2 = Unsafe.Add(ref bufferBase, sourceOffset + 2);
                    int p3 = Unsafe.Add(ref bufferBase, sourceOffset + 3);
                    int p4 = Unsafe.Add(ref bufferBase, sourceOffset + 4);
                    int p5 = Unsafe.Add(ref bufferBase, sourceOffset + BufferStride);
                    int p6 = Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride));
                    for (int pixel = 0; pixel < 8; pixel++)
                    {
                        int tapOffset = pixel * 7;
                        int prediction =
                            (Unsafe.Add(ref taps, tapOffset) * p0)
                            + (Unsafe.Add(ref taps, tapOffset + 1) * p1)
                            + (Unsafe.Add(ref taps, tapOffset + 2) * p2)
                            + (Unsafe.Add(ref taps, tapOffset + 3) * p3)
                            + (Unsafe.Add(ref taps, tapOffset + 4) * p4)
                            + (Unsafe.Add(ref taps, tapOffset + 5) * p5)
                            + (Unsafe.Add(ref taps, tapOffset + 6) * p6);

                        int destinationOffset = ((row + (pixel >> 2)) * BufferStride) + column + (pixel & 3);
                        Unsafe.Add(ref bufferBase, destinationOffset) = (byte)Math.Clamp((prediction + 8) >> 4, 0, maximum);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates every high-bit-depth recursive filter group without hardware intrinsics.
        /// </summary>
        /// <param name="buffer">The initialized recursive workspace.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="maximum">The maximum reconstructed sample.</param>
        /// <param name="taps">The first filter coefficient.</param>
        private static void PredictGroupsScalar(Span<short> buffer, int width, int height, int maximum, ref sbyte taps)
        {
            ref short bufferBase = ref MemoryMarshal.GetReference(buffer);
            for (int row = 1; row <= height; row += 2)
            {
                for (int column = 1; column <= width; column += 4)
                {
                    int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                    int p0 = Unsafe.Add(ref bufferBase, sourceOffset);
                    int p1 = Unsafe.Add(ref bufferBase, sourceOffset + 1);
                    int p2 = Unsafe.Add(ref bufferBase, sourceOffset + 2);
                    int p3 = Unsafe.Add(ref bufferBase, sourceOffset + 3);
                    int p4 = Unsafe.Add(ref bufferBase, sourceOffset + 4);
                    int p5 = Unsafe.Add(ref bufferBase, sourceOffset + BufferStride);
                    int p6 = Unsafe.Add(ref bufferBase, sourceOffset + (2 * BufferStride));
                    for (int pixel = 0; pixel < 8; pixel++)
                    {
                        int tapOffset = pixel * 7;
                        int prediction =
                            (Unsafe.Add(ref taps, tapOffset) * p0)
                            + (Unsafe.Add(ref taps, tapOffset + 1) * p1)
                            + (Unsafe.Add(ref taps, tapOffset + 2) * p2)
                            + (Unsafe.Add(ref taps, tapOffset + 3) * p3)
                            + (Unsafe.Add(ref taps, tapOffset + 4) * p4)
                            + (Unsafe.Add(ref taps, tapOffset + 5) * p5)
                            + (Unsafe.Add(ref taps, tapOffset + 6) * p6);

                        int destinationOffset = ((row + (pixel >> 2)) * BufferStride) + column + (pixel & 3);
                        Unsafe.Add(ref bufferBase, destinationOffset) = (short)Math.Clamp((prediction + 8) >> 4, 0, maximum);
                    }
                }
            }
        }

        /// <summary>
        /// Copies an 8-bit recursive workspace into the strided destination.
        /// </summary>
        /// <param name="buffer">The completed recursive workspace.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void CopyToDestination(ReadOnlySpan<byte> buffer, Span<byte> destination, int destinationStride, int width, int height)
        {
            for (int row = 0; row < height; row++)
            {
                buffer.Slice(((row + 1) * BufferStride) + 1, width).CopyTo(destination.Slice(row * destinationStride, width));
            }
        }

        /// <summary>
        /// Copies a high-bit-depth recursive workspace into the strided destination.
        /// </summary>
        /// <param name="buffer">The completed recursive workspace.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void CopyToDestination(ReadOnlySpan<short> buffer, Span<short> destination, int destinationStride, int width, int height)
        {
            for (int row = 0; row < height; row++)
            {
                buffer.Slice(((row + 1) * BufferStride) + 1, width).CopyTo(destination.Slice(row * destinationStride, width));
            }
        }

        /// <summary>
        /// Copies an 8-bit recursive workspace into the strided destination without vectorized span copying.
        /// </summary>
        /// <param name="buffer">The completed recursive workspace.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void CopyToDestinationScalar(ReadOnlySpan<byte> buffer, Span<byte> destination, int destinationStride, int width, int height)
        {
            for (int row = 0; row < height; row++)
            {
                int sourceOffset = ((row + 1) * BufferStride) + 1;
                int destinationOffset = row * destinationStride;
                for (int column = 0; column < width; column++)
                {
                    destination[destinationOffset + column] = buffer[sourceOffset + column];
                }
            }
        }

        /// <summary>
        /// Copies a high-bit-depth recursive workspace into the strided destination without vectorized span copying.
        /// </summary>
        /// <param name="buffer">The completed recursive workspace.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        private static void CopyToDestinationScalar(ReadOnlySpan<short> buffer, Span<short> destination, int destinationStride, int width, int height)
        {
            for (int row = 0; row < height; row++)
            {
                int sourceOffset = ((row + 1) * BufferStride) + 1;
                int destinationOffset = row * destinationStride;
                for (int column = 0; column < width; column++)
                {
                    destination[destinationOffset + column] = buffer[sourceOffset + column];
                }
            }
        }
    }
}
