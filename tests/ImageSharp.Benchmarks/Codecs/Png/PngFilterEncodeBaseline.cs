// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Png;

/// <summary>
/// Retains the filter-specific PNG encode traversals for direct performance comparison.
/// </summary>
internal static class PngFilterEncodeBaseline
{
    /// <summary>
    /// Executes the filter-specific Sub traversal.
    /// </summary>
    public static void EncodeSub(
        ReadOnlySpan<byte> scanline,
        Span<byte> result,
        int bytesPerPixel,
        out int sum)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;
        resultBaseRef = 1;

        nuint x = 0;

        for (; x < (uint)bytesPerPixel;)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = scan;
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> accumulator = Vector256<int>.Zero;

            for (nuint xLeft = x - (uint)bytesPerPixel; (int)x <= scanline.Length - Vector256<byte>.Count; xLeft += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector256<byte> residual = Avx2.Subtract(scan, left);

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector256<byte>.Count;
                accumulator = Avx2.Add(
                    accumulator,
                    Avx2.SumAbsoluteDifferences(Avx2.Abs(residual.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(accumulator);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<uint> accumulator = Vector<uint>.Zero;

            for (nuint xLeft = x - (uint)bytesPerPixel; (int)x <= scanline.Length - Vector<byte>.Count; xLeft += (uint)Vector<byte>.Count)
            {
                Vector<byte> scan = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector<byte> left = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector<byte> residual = scan - left;

                Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector<byte>.Count;
                Numerics.Accumulate(
                    ref accumulator,
                    Vector.AsVectorByte(Vector.Abs(Vector.AsVectorSByte(residual))));
            }

            for (int i = 0; i < Vector<uint>.Count; i++)
            {
                sum += (int)accumulator[i];
            }
        }

        for (nuint xLeft = x - (uint)bytesPerPixel; x < (uint)scanline.Length; xLeft++)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, xLeft);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - left);
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }
    }

    /// <summary>
    /// Executes the filter-specific Up traversal.
    /// </summary>
    public static void EncodeUp(
        ReadOnlySpan<byte> scanline,
        ReadOnlySpan<byte> previousScanline,
        Span<byte> result,
        out int sum)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;
        resultBaseRef = 2;

        nuint x = 0;

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> accumulator = Vector256<int>.Zero;

            for (; (int)x <= scanline.Length - Vector256<byte>.Count;)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector256<byte> residual = Avx2.Subtract(scan, above);

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector256<byte>.Count;
                accumulator = Avx2.Add(
                    accumulator,
                    Avx2.SumAbsoluteDifferences(Avx2.Abs(residual.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(accumulator);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<uint> accumulator = Vector<uint>.Zero;

            for (; (int)x <= scanline.Length - Vector<byte>.Count;)
            {
                Vector<byte> scan = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector<byte> above = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector<byte> residual = scan - above;

                Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector<byte>.Count;
                Numerics.Accumulate(
                    ref accumulator,
                    Vector.AsVectorByte(Vector.Abs(Vector.AsVectorSByte(residual))));
            }

            for (int i = 0; i < Vector<uint>.Count; i++)
            {
                sum += (int)accumulator[i];
            }
        }

        for (; x < (uint)scanline.Length;)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - above);
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }
    }

    /// <summary>
    /// Executes the filter-specific Average traversal.
    /// </summary>
    public static void EncodeAverage(
        ReadOnlySpan<byte> scanline,
        ReadOnlySpan<byte> previousScanline,
        Span<byte> result,
        uint bytesPerPixel,
        out int sum)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;
        resultBaseRef = 3;

        nuint x = 0;

        for (; x < bytesPerPixel;)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - (above >> 1));
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> accumulator = Vector256<int>.Zero;
            Vector256<byte> allBitsSet = Avx2.CompareEqual(accumulator, accumulator).AsByte();

            for (nuint xLeft = x - bytesPerPixel; (int)x <= scanline.Length - Vector256<byte>.Count; xLeft += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector256<byte> average = Avx2.Xor(
                    Avx2.Average(Avx2.Xor(left, allBitsSet), Avx2.Xor(above, allBitsSet)),
                    allBitsSet);

                Vector256<byte> residual = Avx2.Subtract(scan, average);
                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector256<byte>.Count;
                accumulator = Avx2.Add(
                    accumulator,
                    Avx2.SumAbsoluteDifferences(Avx2.Abs(residual.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(accumulator);
        }
        else if (Sse2.IsSupported)
        {
            Vector128<byte> zero = Vector128<byte>.Zero;
            Vector128<int> accumulator = Vector128<int>.Zero;
            Vector128<byte> allBitsSet = Sse2.CompareEqual(accumulator, accumulator).AsByte();

            for (nuint xLeft = x - bytesPerPixel; (int)x <= scanline.Length - Vector128<byte>.Count; xLeft += (uint)Vector128<byte>.Count)
            {
                Vector128<byte> scan = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector128<byte> left = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector128<byte> above = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector128<byte> average = Sse2.Xor(
                    Sse2.Average(Sse2.Xor(left, allBitsSet), Sse2.Xor(above, allBitsSet)),
                    allBitsSet);

                Vector128<byte> residual = Sse2.Subtract(scan, average);
                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector128<byte>.Count;

                Vector128<byte> absolute;

                if (Ssse3.IsSupported)
                {
                    absolute = Ssse3.Abs(residual.AsSByte());
                }
                else
                {
                    Vector128<sbyte> mask = Sse2.CompareGreaterThan(zero.AsSByte(), residual.AsSByte());
                    absolute = Sse2.Xor(Sse2.Add(residual.AsSByte(), mask), mask).AsByte();
                }

                accumulator = Sse2.Add(
                    accumulator,
                    Sse2.SumAbsoluteDifferences(absolute, zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(accumulator);
        }

        for (nuint xLeft = x - bytesPerPixel; x < (uint)scanline.Length; xLeft++)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, xLeft);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - ((left + above) >> 1));
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }
    }

    /// <summary>
    /// Executes the filter-specific Paeth traversal.
    /// </summary>
    public static void EncodePaeth(
        ReadOnlySpan<byte> scanline,
        ReadOnlySpan<byte> previousScanline,
        Span<byte> result,
        int bytesPerPixel,
        out int sum)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;
        resultBaseRef = 4;

        nuint x = 0;

        for (; x < (uint)bytesPerPixel;)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - above);
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> accumulator = Vector256<int>.Zero;

            for (nuint xLeft = x - (uint)bytesPerPixel; (int)x <= scanline.Length - Vector256<byte>.Count; xLeft += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector256<byte> upperLeft = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft));
                Vector256<byte> residual = Avx2.Subtract(scan, PaethPredictor(left, above, upperLeft));

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector256<byte>.Count;
                accumulator = Avx2.Add(
                    accumulator,
                    Avx2.SumAbsoluteDifferences(Avx2.Abs(residual.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(accumulator);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<uint> accumulator = Vector<uint>.Zero;

            for (nuint xLeft = x - (uint)bytesPerPixel; (int)x <= scanline.Length - Vector<byte>.Count; xLeft += (uint)Vector<byte>.Count)
            {
                Vector<byte> scan = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector<byte> left = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector<byte> above = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector<byte> upperLeft = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft));
                Vector<byte> residual = scan - PaethPredictor(left, above, upperLeft);

                Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = residual;
                x += (uint)Vector<byte>.Count;
                Numerics.Accumulate(
                    ref accumulator,
                    Vector.AsVectorByte(Vector.Abs(Vector.AsVectorSByte(residual))));
            }

            for (int i = 0; i < Vector<uint>.Count; i++)
            {
                sum += (int)accumulator[i];
            }
        }

        for (nuint xLeft = x - (uint)bytesPerPixel; x < (uint)scanline.Length; xLeft++)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, xLeft);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            byte upperLeft = Unsafe.Add(ref previousBaseRef, xLeft);
            x++;
            ref byte residual = ref Unsafe.Add(ref resultBaseRef, x);
            residual = (byte)(scan - PaethPredictor(left, above, upperLeft));
            sum += Numerics.Abs(unchecked((sbyte)residual));
        }
    }

    /// <summary>
    /// Selects the scalar Paeth predictor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PaethPredictor(byte left, byte above, byte upperLeft)
    {
        int p = left + above - upperLeft;
        int distanceLeft = Numerics.Abs(p - left);
        int distanceAbove = Numerics.Abs(p - above);
        int distanceUpperLeft = Numerics.Abs(p - upperLeft);

        if (distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft)
        {
            return left;
        }

        return distanceAbove <= distanceUpperLeft ? above : upperLeft;
    }

    /// <summary>
    /// Selects the AVX2 Paeth predictor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> PaethPredictor(
        Vector256<byte> left,
        Vector256<byte> above,
        Vector256<byte> upperLeft)
    {
        Vector256<byte> zero = Vector256<byte>.Zero;
        Vector256<byte> aboveMinusUpper = Avx2.SubtractSaturate(above, upperLeft);
        Vector256<byte> leftMinusUpper = Avx2.SubtractSaturate(left, upperLeft);
        Vector256<byte> distanceLeft =
            Avx2.Or(Avx2.SubtractSaturate(upperLeft, above), aboveMinusUpper);

        Vector256<byte> distanceAbove =
            Avx2.Or(Avx2.SubtractSaturate(upperLeft, left), leftMinusUpper);

        Vector256<byte> sameDirection = Avx2.CompareEqual(
            Avx2.CompareEqual(aboveMinusUpper, zero),
            Avx2.CompareEqual(leftMinusUpper, zero));

        Vector256<byte> distanceUpper = Avx2.Or(
            sameDirection,
            Avx2.Or(
                Avx2.SubtractSaturate(distanceAbove, distanceLeft),
                Avx2.SubtractSaturate(distanceLeft, distanceAbove)));

        Vector256<byte> minimumAboveUpper = Avx2.Min(distanceUpper, distanceAbove);
        Vector256<byte> aboveOrUpper = Avx2.BlendVariable(
            upperLeft,
            above,
            Avx2.CompareEqual(minimumAboveUpper, distanceAbove));

        return Avx2.BlendVariable(
            aboveOrUpper,
            left,
            Avx2.CompareEqual(Avx2.Min(minimumAboveUpper, distanceLeft), distanceLeft));
    }

    /// <summary>
    /// Selects the portable vector Paeth predictor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<byte> PaethPredictor(
        Vector<byte> left,
        Vector<byte> above,
        Vector<byte> upperLeft)
    {
        Vector.Widen(left, out Vector<ushort> leftLow, out Vector<ushort> leftHigh);
        Vector.Widen(above, out Vector<ushort> aboveLow, out Vector<ushort> aboveHigh);
        Vector.Widen(upperLeft, out Vector<ushort> upperLow, out Vector<ushort> upperHigh);

        Vector<short> lower = PaethPredictor(
            Vector.AsVectorInt16(leftLow),
            Vector.AsVectorInt16(aboveLow),
            Vector.AsVectorInt16(upperLow));

        Vector<short> upper = PaethPredictor(
            Vector.AsVectorInt16(leftHigh),
            Vector.AsVectorInt16(aboveHigh),
            Vector.AsVectorInt16(upperHigh));

        return Vector.AsVectorByte(Vector.Narrow(lower, upper));
    }

    /// <summary>
    /// Selects the portable widened Paeth predictor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<short> PaethPredictor(
        Vector<short> left,
        Vector<short> above,
        Vector<short> upperLeft)
    {
        Vector<short> p = left + above - upperLeft;
        Vector<short> distanceLeft = Vector.Abs(p - left);
        Vector<short> distanceAbove = Vector.Abs(p - above);
        Vector<short> distanceUpper = Vector.Abs(p - upperLeft);

        Vector<short> chooseLeft = Vector.BitwiseAnd(
            Vector.LessThanOrEqual(distanceLeft, distanceAbove),
            Vector.LessThanOrEqual(distanceLeft, distanceUpper));

        return Vector.ConditionalSelect(
            chooseLeft,
            left,
            Vector.ConditionalSelect(
                Vector.LessThanOrEqual(distanceAbove, distanceUpper),
                above,
                upperLeft));
    }
}
