// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Provides the shared HEVC inverse-transform stage and transpose operations. Vector lanes represent independent
/// transform lines, while consecutive scratch rows represent frequency groups in the partial-butterfly factorization.
/// Arithmetic never mixes lines; transposition is the only operation that exchanges row and column coordinates.
/// </content>
internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// Gets the common inverse-DCT magnitudes ordered on the pi-over-sixty-four angle grid.
    /// </summary>
    private static ReadOnlySpan<sbyte> DiscreteCosineMagnitudes =>
    [
        90, 90, 90, 90, 89, 88, 87, 85, 83, 82, 80, 78, 75, 73, 70, 67, 64,
        61, 57, 54, 50, 46, 43, 38, 36, 31, 25, 22, 18, 13, 9, 4, 0
    ];

    /// <summary>
    /// Calculates the disjoint odd-frequency groups that seed the HEVC partial-butterfly reconstruction.
    /// </summary>
    /// <typeparam name="TOperator">The selected inverse-DCT operator.</typeparam>
    /// <param name="source">The frequency rows followed by contiguous independent lines.</param>
    /// <param name="groups">The destination group rows.</param>
    /// <param name="lineCount">The number of independent lines transformed together.</param>
    private static void PopulateButterflyGroups<TOperator>(ReadOnlySpan<int> source, Span<int> groups, int lineCount)
        where TOperator : struct, IHevcInverseTransformOperator
    {
        int size = TOperator.Size;
        int groupOffset = 0;
        for (int frequencyStep = 2; frequencyStep < size; frequencyStep <<= 1)
        {
            int outputCount = size / frequencyStep;
            int firstFrequency = frequencyStep >> 1;
            for (int position = 0; position < outputCount; position++)
            {
                PopulateButterflyGroupRow<TOperator>(
                    source,
                    groups.Slice((groupOffset + position) * lineCount, lineCount),
                    lineCount,
                    firstFrequency,
                    frequencyStep,
                    position);
            }

            groupOffset += outputCount;
        }

        // The deepest even group contains the DC term and the transform's Nyquist-frequency term. It remains a
        // two-element group for every supported DCT size and closes the recursive butterfly hierarchy.
        for (int position = 0; position < 2; position++)
        {
            PopulateButterflyGroupRow<TOperator>(
                source,
                groups.Slice((groupOffset + position) * lineCount, lineCount),
                lineCount,
                0,
                size >> 1,
                position);
        }
    }

    /// <summary>
    /// Calculates one partial-butterfly group row across all independent lines.
    /// </summary>
    /// <typeparam name="TOperator">The selected inverse-DCT operator.</typeparam>
    /// <param name="source">The complete frequency-row input.</param>
    /// <param name="destination">The destination group row.</param>
    /// <param name="lineCount">The number of independent lines.</param>
    /// <param name="firstFrequency">The first frequency included by the group.</param>
    /// <param name="frequencyStep">The distance between included frequencies.</param>
    /// <param name="position">The group-relative spatial coordinate.</param>
    private static void PopulateButterflyGroupRow<TOperator>(
        ReadOnlySpan<int> source,
        Span<int> destination,
        int lineCount,
        int firstFrequency,
        int frequencyStep,
        int position)
        where TOperator : struct, IHevcInverseTransformOperator
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(source);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        int x = 0;

        // Source storage is frequency-major: advancing one lane moves to the same frequency in another independent
        // transform line. The shared X offset lets each narrower width continue exactly where the wider loop stopped.
        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = lineCount - Vector512<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector512<int>.Count)
            {
                Vector512<int> sum = Vector512<int>.Zero;
                for (int frequency = firstFrequency; frequency < TOperator.Size; frequency += frequencyStep)
                {
                    Vector512<int> values = Vector512.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                    sum += values * Vector512.Create(TOperator.GetCoefficient(frequency, position));
                }

                sum.StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = lineCount - Vector256<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector256<int>.Count)
            {
                Vector256<int> sum = Vector256<int>.Zero;
                for (int frequency = firstFrequency; frequency < TOperator.Size; frequency += frequencyStep)
                {
                    Vector256<int> values = Vector256.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                    sum += values * Vector256.Create(TOperator.GetCoefficient(frequency, position));
                }

                sum.StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = lineCount - Vector128<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector128<int>.Count)
            {
                Vector128<int> sum = Vector128<int>.Zero;
                for (int frequency = firstFrequency; frequency < TOperator.Size; frequency += frequencyStep)
                {
                    Vector128<int> values = Vector128.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                    sum += values * Vector128.Create(TOperator.GetCoefficient(frequency, position));
                }

                sum.StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        for (; x < lineCount; x++)
        {
            int sum = 0;
            for (int frequency = firstFrequency; frequency < TOperator.Size; frequency += frequencyStep)
            {
                sum += source[(frequency * lineCount) + x] * TOperator.GetCoefficient(frequency, position);
            }

            destination[x] = sum;
        }
    }

    /// <summary>
    /// Expands the disjoint partial-butterfly groups into spatial rows.
    /// </summary>
    /// <typeparam name="TOperator">The selected inverse-DCT operator.</typeparam>
    /// <param name="initial">The buffer containing every disjoint group.</param>
    /// <param name="alternate">The alternate expansion buffer.</param>
    /// <param name="lineCount">The number of independent lines transformed together.</param>
    /// <param name="shift">The rounded right shift applied at the final hierarchy level.</param>
    /// <param name="minimum">The inclusive output minimum.</param>
    /// <param name="maximum">The inclusive output maximum.</param>
    /// <returns>The buffer containing the completed spatial rows.</returns>
    private static Span<int> CombineButterflyGroups<TOperator>(
        Span<int> initial,
        Span<int> alternate,
        int lineCount,
        int shift,
        int minimum,
        int maximum)
        where TOperator : struct, IHevcInverseTransformOperator
    {
        int size = TOperator.Size;
        int combinedSize = 2;
        int combinedStart = size - combinedSize;
        bool currentIsInitial = true;
        while (combinedSize < size)
        {
            int oddStart = combinedStart - combinedSize;
            bool finalLevel = (combinedSize << 1) == size;
            ReadOnlySpan<int> even = currentIsInitial ? initial : alternate;
            Span<int> destination = currentIsInitial ? alternate : initial;

            // Odd rows remain in the initial disjoint-group buffer while expanded even rows alternate buffers. This
            // preserves every source row needed by later hierarchy levels without allocating another transform block.
            for (int position = 0; position < combinedSize; position++)
            {
                ReadOnlySpan<int> evenRow = even.Slice((combinedStart + position) * lineCount, lineCount);
                ReadOnlySpan<int> oddRow = initial.Slice((oddStart + position) * lineCount, lineCount);
                Span<int> positiveRow = destination.Slice((oddStart + position) * lineCount, lineCount);
                Span<int> negativeRow = destination.Slice((oddStart + (2 * combinedSize) - 1 - position) * lineCount, lineCount);
                CombineButterflyRows(evenRow, oddRow, positiveRow, negativeRow, finalLevel, shift, minimum, maximum);
            }

            combinedStart = oddStart;
            combinedSize <<= 1;
            currentIsInitial = !currentIsInitial;
        }

        return currentIsInitial ? initial : alternate;
    }

    /// <summary>
    /// Combines one symmetric pair of partial-butterfly rows.
    /// </summary>
    /// <param name="even">The even-frequency contribution.</param>
    /// <param name="odd">The odd-frequency contribution.</param>
    /// <param name="positive">The destination receiving the added contribution.</param>
    /// <param name="negative">The destination receiving the subtracted contribution.</param>
    /// <param name="roundAndClip">Whether this is the final hierarchy level.</param>
    /// <param name="shift">The rounded right shift applied at the final hierarchy level.</param>
    /// <param name="minimum">The inclusive output minimum.</param>
    /// <param name="maximum">The inclusive output maximum.</param>
    private static void CombineButterflyRows(
        ReadOnlySpan<int> even,
        ReadOnlySpan<int> odd,
        Span<int> positive,
        Span<int> negative,
        bool roundAndClip,
        int shift,
        int minimum,
        int maximum)
    {
        ref int evenBase = ref MemoryMarshal.GetReference(even);
        ref int oddBase = ref MemoryMarshal.GetReference(odd);
        ref int positiveBase = ref MemoryMarshal.GetReference(positive);
        ref int negativeBase = ref MemoryMarshal.GetReference(negative);
        int x = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = even.Length - Vector512<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector512<int>.Count)
            {
                Vector512<int> evenValues = Vector512.LoadUnsafe(ref evenBase, (nuint)x);
                Vector512<int> oddValues = Vector512.LoadUnsafe(ref oddBase, (nuint)x);
                Vector512<int> added = evenValues + oddValues;
                Vector512<int> subtracted = evenValues - oddValues;
                if (roundAndClip)
                {
                    added = RoundShiftAndClamp(added, shift, minimum, maximum);
                    subtracted = RoundShiftAndClamp(subtracted, shift, minimum, maximum);
                }

                added.StoreUnsafe(ref positiveBase, (nuint)x);
                subtracted.StoreUnsafe(ref negativeBase, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = even.Length - Vector256<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector256<int>.Count)
            {
                Vector256<int> evenValues = Vector256.LoadUnsafe(ref evenBase, (nuint)x);
                Vector256<int> oddValues = Vector256.LoadUnsafe(ref oddBase, (nuint)x);
                Vector256<int> added = evenValues + oddValues;
                Vector256<int> subtracted = evenValues - oddValues;
                if (roundAndClip)
                {
                    added = RoundShiftAndClamp(added, shift, minimum, maximum);
                    subtracted = RoundShiftAndClamp(subtracted, shift, minimum, maximum);
                }

                added.StoreUnsafe(ref positiveBase, (nuint)x);
                subtracted.StoreUnsafe(ref negativeBase, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = even.Length - Vector128<int>.Count;
            for (; x <= oneVectorFromEnd; x += Vector128<int>.Count)
            {
                Vector128<int> evenValues = Vector128.LoadUnsafe(ref evenBase, (nuint)x);
                Vector128<int> oddValues = Vector128.LoadUnsafe(ref oddBase, (nuint)x);
                Vector128<int> added = evenValues + oddValues;
                Vector128<int> subtracted = evenValues - oddValues;
                if (roundAndClip)
                {
                    added = RoundShiftAndClamp(added, shift, minimum, maximum);
                    subtracted = RoundShiftAndClamp(subtracted, shift, minimum, maximum);
                }

                added.StoreUnsafe(ref positiveBase, (nuint)x);
                subtracted.StoreUnsafe(ref negativeBase, (nuint)x);
            }
        }

        for (; x < even.Length; x++)
        {
            int added = even[x] + odd[x];
            int subtracted = even[x] - odd[x];
            if (roundAndClip)
            {
                added = RoundShiftAndClamp(added, shift, minimum, maximum);
                subtracted = RoundShiftAndClamp(subtracted, shift, minimum, maximum);
            }

            positive[x] = added;
            negative[x] = subtracted;
        }
    }

    /// <summary>
    /// Applies the four-point inverse-DST matrix across all independent lines.
    /// </summary>
    /// <typeparam name="TOperator">The selected dense transform operator.</typeparam>
    /// <param name="source">The frequency rows followed by contiguous independent lines.</param>
    /// <param name="destination">The spatial rows followed by contiguous independent lines.</param>
    /// <param name="lineCount">The number of independent lines transformed together.</param>
    /// <param name="shift">The rounded right shift applied to each result.</param>
    /// <param name="minimum">The inclusive output minimum.</param>
    /// <param name="maximum">The inclusive output maximum.</param>
    private static void TransformDense<TOperator>(
        ReadOnlySpan<int> source,
        Span<int> destination,
        int lineCount,
        int shift,
        int minimum,
        int maximum)
        where TOperator : struct, IHevcInverseTransformOperator
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(source);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        for (int position = 0; position < TOperator.Size; position++)
        {
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = lineCount - Vector512<int>.Count;
                for (; x <= oneVectorFromEnd; x += Vector512<int>.Count)
                {
                    Vector512<int> sum = Vector512<int>.Zero;
                    for (int frequency = 0; frequency < TOperator.Size; frequency++)
                    {
                        Vector512<int> values = Vector512.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                        sum += values * Vector512.Create(TOperator.GetCoefficient(frequency, position));
                    }

                    RoundShiftAndClamp(sum, shift, minimum, maximum).StoreUnsafe(ref destinationBase, (nuint)((position * lineCount) + x));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = lineCount - Vector256<int>.Count;
                for (; x <= oneVectorFromEnd; x += Vector256<int>.Count)
                {
                    Vector256<int> sum = Vector256<int>.Zero;
                    for (int frequency = 0; frequency < TOperator.Size; frequency++)
                    {
                        Vector256<int> values = Vector256.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                        sum += values * Vector256.Create(TOperator.GetCoefficient(frequency, position));
                    }

                    RoundShiftAndClamp(sum, shift, minimum, maximum).StoreUnsafe(ref destinationBase, (nuint)((position * lineCount) + x));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = lineCount - Vector128<int>.Count;
                for (; x <= oneVectorFromEnd; x += Vector128<int>.Count)
                {
                    Vector128<int> sum = Vector128<int>.Zero;
                    for (int frequency = 0; frequency < TOperator.Size; frequency++)
                    {
                        Vector128<int> values = Vector128.LoadUnsafe(ref sourceBase, (nuint)((frequency * lineCount) + x));
                        sum += values * Vector128.Create(TOperator.GetCoefficient(frequency, position));
                    }

                    RoundShiftAndClamp(sum, shift, minimum, maximum).StoreUnsafe(ref destinationBase, (nuint)((position * lineCount) + x));
                }
            }

            for (; x < lineCount; x++)
            {
                int sum = 0;
                for (int frequency = 0; frequency < TOperator.Size; frequency++)
                {
                    sum += source[(frequency * lineCount) + x] * TOperator.GetCoefficient(frequency, position);
                }

                destination[(position * lineCount) + x] = RoundShiftAndClamp(sum, shift, minimum, maximum);
            }
        }
    }

    /// <summary>
    /// Transposes one rectangular transform block into a separate full-block buffer.
    /// </summary>
    /// <param name="source">The source block in raster order.</param>
    /// <param name="destination">The transposed destination block.</param>
    /// <param name="sourceHeight">The source row count.</param>
    /// <param name="sourceWidth">The source column count.</param>
    private static void Transpose(ReadOnlySpan<int> source, Span<int> destination, int sourceHeight, int sourceWidth)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref int sourceBase = ref MemoryMarshal.GetReference(source);
            ref int destinationBase = ref MemoryMarshal.GetReference(destination);

            // Every supported HEVC transform dimension is a multiple of four. Complete four-by-four tiles therefore
            // transpose the rectangular block without masked loads, partial stores, or access to row padding.
            for (int y = 0; y < sourceHeight; y += 4)
            {
                for (int x = 0; x < sourceWidth; x += 4)
                {
                    Vector128<int> row0 = Vector128.LoadUnsafe(ref sourceBase, (nuint)((y * sourceWidth) + x));
                    Vector128<int> row1 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 1) * sourceWidth) + x));
                    Vector128<int> row2 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 2) * sourceWidth) + x));
                    Vector128<int> row3 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 3) * sourceWidth) + x));
                    Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
                    row0.StoreUnsafe(ref destinationBase, (nuint)((x * sourceHeight) + y));
                    row1.StoreUnsafe(ref destinationBase, (nuint)(((x + 1) * sourceHeight) + y));
                    row2.StoreUnsafe(ref destinationBase, (nuint)(((x + 2) * sourceHeight) + y));
                    row3.StoreUnsafe(ref destinationBase, (nuint)(((x + 3) * sourceHeight) + y));
                }
            }

            return;
        }

        for (int y = 0; y < sourceHeight; y++)
        {
            for (int x = 0; x < sourceWidth; x++)
            {
                destination[(x * sourceHeight) + y] = source[(y * sourceWidth) + x];
            }
        }
    }

    /// <summary>
    /// Adds a complete signed residual block to the predicted samples and clips to the component precision.
    /// </summary>
    /// <param name="residual">The signed residual block in raster order.</param>
    /// <param name="destination">The predicted samples beginning at the block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    public static void AddResidual(ReadOnlySpan<int> residual, Span<ushort> destination, int destinationStride, int width, int height, int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<int> residualRow = residual.Slice(y * width, width);
            Span<ushort> destinationRow = destination.Slice(y * destinationStride, width);
            ref int residualBase = ref MemoryMarshal.GetReference(residualRow);
            ref ushort destinationBase = ref MemoryMarshal.GetReference(destinationRow);
            int x = 0;

            if (Vector256.IsHardwareAccelerated)
            {
                // Eight UInt16 predictions widen into one Int32 vector so residual addition cannot overflow sample
                // storage. Narrowing occurs only after clipping and stores exactly the eight logical destination values.
                int oneVectorFromEnd = width - Vector256<int>.Count;
                for (; x <= oneVectorFromEnd; x += Vector256<int>.Count)
                {
                    Vector128<ushort> predicted16 = Vector128.LoadUnsafe(ref destinationBase, (nuint)x);
                    Vector256<int> predicted = Vector256.Create(Vector128.WidenLower(predicted16), Vector128.WidenUpper(predicted16)).AsInt32();
                    Vector256<int> reconstructed = Vector256.Clamp(
                        predicted + Vector256.LoadUnsafe(ref residualBase, (nuint)x),
                        Vector256<int>.Zero,
                        Vector256.Create(maximum));

                    Vector128.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32()).StoreUnsafe(ref destinationBase, (nuint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // The four-sample path uses exact 64-bit loads and stores; it does not depend on writable row padding.
                int oneVectorFromEnd = width - Vector128<int>.Count;
                for (; x <= oneVectorFromEnd; x += Vector128<int>.Count)
                {
                    ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, x)));
                    Vector128<int> predicted = Vector128.WidenLower(Vector128.CreateScalar(packed).AsUInt16()).AsInt32();
                    Vector128<int> reconstructed = Vector128.Clamp(
                        predicted + Vector128.LoadUnsafe(ref residualBase, (nuint)x),
                        Vector128<int>.Zero,
                        Vector128.Create(maximum));

                    Vector128<ushort> narrowed = Vector128.Narrow(reconstructed.AsUInt32(), Vector128<uint>.Zero);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, x)), narrowed.AsUInt64().ToScalar());
                }
            }

            for (; x < width; x++)
            {
                destinationRow[x] = (ushort)Math.Clamp(destinationRow[x] + residualRow[x], 0, maximum);
            }
        }
    }

    /// <summary>
    /// Applies HEVC's rounded right shift and inclusive clipping to a 512-bit vector.
    /// </summary>
    /// <param name="value">The unnormalized transform values.</param>
    /// <param name="shift">The right-shift count.</param>
    /// <param name="minimum">The inclusive result minimum.</param>
    /// <param name="maximum">The inclusive result maximum.</param>
    /// <returns>The normalized and clipped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> RoundShiftAndClamp(Vector512<int> value, int shift, int minimum, int maximum)
        => Vector512.Clamp((value + Vector512.Create(1 << (shift - 1))) >> shift, Vector512.Create(minimum), Vector512.Create(maximum));

    /// <summary>
    /// Applies HEVC's rounded right shift and inclusive clipping to a 256-bit vector.
    /// </summary>
    /// <param name="value">The unnormalized transform values.</param>
    /// <param name="shift">The right-shift count.</param>
    /// <param name="minimum">The inclusive result minimum.</param>
    /// <param name="maximum">The inclusive result maximum.</param>
    /// <returns>The normalized and clipped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> RoundShiftAndClamp(Vector256<int> value, int shift, int minimum, int maximum)
        => Vector256.Clamp((value + Vector256.Create(1 << (shift - 1))) >> shift, Vector256.Create(minimum), Vector256.Create(maximum));

    /// <summary>
    /// Applies HEVC's rounded right shift and inclusive clipping to a 128-bit vector.
    /// </summary>
    /// <param name="value">The unnormalized transform values.</param>
    /// <param name="shift">The right-shift count.</param>
    /// <param name="minimum">The inclusive result minimum.</param>
    /// <param name="maximum">The inclusive result maximum.</param>
    /// <returns>The normalized and clipped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> RoundShiftAndClamp(Vector128<int> value, int shift, int minimum, int maximum)
        => Vector128.Clamp((value + Vector128.Create(1 << (shift - 1))) >> shift, Vector128.Create(minimum), Vector128.Create(maximum));

    /// <summary>
    /// Applies HEVC's rounded right shift and inclusive clipping to one scalar value.
    /// </summary>
    /// <param name="value">The unnormalized transform value.</param>
    /// <param name="shift">The right-shift count.</param>
    /// <param name="minimum">The inclusive result minimum.</param>
    /// <param name="maximum">The inclusive result maximum.</param>
    /// <returns>The normalized and clipped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundShiftAndClamp(int value, int shift, int minimum, int maximum)
        => Math.Clamp((value + (1 << (shift - 1))) >> shift, minimum, maximum);
}
