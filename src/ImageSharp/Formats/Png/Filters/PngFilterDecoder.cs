// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// Reconstructs the bytes of a scanline from the residuals of a filter operator.
/// </summary>
/// <remarks>
/// <para>
/// Each reconstructed byte is residual + predictor, modulo 256. The Sub, Average and Paeth predictors read the
/// reconstructed byte of the pixel to the left, so the pixels reconstruct one after the other. One step handles all
/// bytes of one pixel. For the Average and Paeth predictors, each byte has a 16-bit lane of a
/// <see cref="Vector128{T}"/>, which gives the shortest dependency chain. The Sub predictor is the left pixel, so each
/// byte keeps a byte lane and the chain is one byte add. A wider vector holds more pixels. But each pixel must wait for the pixel to its left, so a wider vector does
/// not reduce the step count.
/// </para>
/// <para>
/// A pixel of 1 byte uses one lane. The vector step keeps the left byte in a register and uses the branchless
/// predictor. Thus it does not wait on a store and a load for each byte. The Up predictor does not read the left pixel,
/// so each byte of an Up scanline reconstructs on its own, in the widest vectors.
/// </para>
/// </remarks>
internal static partial class PngFilterDecoder
{
    /// <summary>
    /// Reconstructs a scanline that was filtered with <typeparamref name="TOperator"/>.
    /// </summary>
    /// <typeparam name="TOperator">The filter operator.</typeparam>
    /// <param name="scanline">The scanline, with the filter type byte first. The bytes after it are reconstructed in place.</param>
    /// <param name="previousScanline">The reconstructed preceding scanline, with the same length. Not read when the filter does not use it.</param>
    /// <param name="bytesPerPixel">The byte count of one pixel, or 1 for bit depths of less than 8.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decode<TOperator>(Span<byte> scanline, ReadOnlySpan<byte> previousScanline, int bytesPerPixel)
        where TOperator : struct, IPngFilterOperator
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));

        if (!TOperator.UsesLeft)
        {
            DecodeWithoutLeft<TOperator>(scanline, previousScanline);
            return;
        }

        if (!Vector128.IsHardwareAccelerated)
        {
            DecodeScalar<TOperator>(scanline, previousScanline, (uint)bytesPerPixel);
            return;
        }

        // A bit depth of 8 or 16 gives a whole number of bytes for each pixel: 1, 2, 3, 4, 6 or 8. A bit depth of less
        // than 8 filters with 1 byte for each pixel.
        switch (bytesPerPixel)
        {
            case 1:
                Decode<TOperator, Pixel8Operator>(scanline, previousScanline);
                break;
            case 2:
                Decode<TOperator, Pixel16Operator>(scanline, previousScanline);
                break;
            case 3:
                Decode<TOperator, Pixel24Operator>(scanline, previousScanline);
                break;
            case 4:
                Decode<TOperator, Pixel32Operator>(scanline, previousScanline);
                break;
            case 6:
                Decode<TOperator, Pixel48Operator>(scanline, previousScanline);
                break;
            default:
                Decode<TOperator, Pixel64Operator>(scanline, previousScanline);
                break;
        }
    }

    /// <summary>
    /// Reconstructs a scanline that was filtered with a predictor that does not read the left pixel.
    /// </summary>
    /// <remarks>
    /// No predictor input is a reconstructed byte of the scanline, so each byte reconstructs on its own. Thus the row
    /// uses the widest vectors, from the same offset in each width, and the scalar predictor for the remainder.
    /// </remarks>
    /// <typeparam name="TOperator">The filter operator.</typeparam>
    /// <param name="scanline">The scanline, with the filter type byte first.</param>
    /// <param name="previousScanline">The reconstructed preceding scanline.</param>
    private static void DecodeWithoutLeft<TOperator>(Span<byte> scanline, ReadOnlySpan<byte> previousScanline)
        where TOperator : struct, IPngFilterOperator
    {
        ref byte row = ref MemoryMarshal.GetReference(scanline);
        ref byte previousRow = ref MemoryMarshal.GetReference(previousScanline);
        nuint length = (uint)scanline.Length;

        // Byte 0 is the filter type.
        nuint x = 1;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector512<byte>.Count <= length; x += (uint)Vector512<byte>.Count)
            {
                Vector512<byte> above = TOperator.UsesAbove ? Vector512.LoadUnsafe(ref previousRow, x) : default;
                (Vector512.LoadUnsafe(ref row, x) + TOperator.Predict(default, above, default)).StoreUnsafe(ref row, x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector256<byte>.Count <= length; x += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> above = TOperator.UsesAbove ? Vector256.LoadUnsafe(ref previousRow, x) : default;
                (Vector256.LoadUnsafe(ref row, x) + TOperator.Predict(default, above, default)).StoreUnsafe(ref row, x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector128<byte>.Count <= length; x += (uint)Vector128<byte>.Count)
            {
                Vector128<byte> above = TOperator.UsesAbove ? Vector128.LoadUnsafe(ref previousRow, x) : default;
                (Vector128.LoadUnsafe(ref row, x) + TOperator.Predict(default, above, default)).StoreUnsafe(ref row, x);
            }
        }

        for (; x < length; x++)
        {
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousRow, x) : (byte)0;
            ref byte scan = ref Unsafe.Add(ref row, x);
            scan = (byte)(scan + TOperator.Predict(0, above, 0));
        }
    }

    /// <summary>
    /// Reconstructs a scanline one pixel at a time in the low lanes of a vector.
    /// </summary>
    /// <typeparam name="TOperator">The filter operator.</typeparam>
    /// <typeparam name="TPixelOperator">The pixel operator.</typeparam>
    /// <param name="scanline">The scanline, with the filter type byte first.</param>
    /// <param name="previousScanline">The reconstructed preceding scanline.</param>
    private static void Decode<TOperator, TPixelOperator>(Span<byte> scanline, ReadOnlySpan<byte> previousScanline)
        where TOperator : struct, IPngFilterOperator
        where TPixelOperator : struct, IPixelOperator
    {
        // The bytes after the filter type byte are a whole number of pixels.
        DebugGuard.IsTrue((scanline.Length - 1) % TPixelOperator.BytesPerPixel == 0, "The scanline must hold whole pixels.");

        ref byte row = ref Unsafe.Add(ref MemoryMarshal.GetReference(scanline), 1);
        ref byte previousRow = ref Unsafe.Add(ref MemoryMarshal.GetReference(previousScanline), 1);
        nuint width = (uint)((scanline.Length - 1) / TPixelOperator.BytesPerPixel);

        // A predictor that does not read the row above is the left pixel itself. Its byte add wraps modulo 256 as
        // PNG requires, so the byte lanes need no 16-bit widening. The dependency chain is one byte add for each pixel.
        if (!TOperator.UsesAbove)
        {
            Vector128<byte> leftBytes = Vector128<byte>.Zero;
            for (nuint x = 0; x < width; x++)
            {
                leftBytes = TPixelOperator.Load(ref row, x) + TOperator.Predict(leftBytes, default, default);
                TPixelOperator.Store(ref row, x, leftBytes);
            }

            return;
        }

        // Lane layout: component i of the pixel is 16-bit lane i, with its byte value in the low byte and 0 in the
        // high byte. A pixel has at most 8 components, so it fits one vector. The predictor uses 16-bit math, which
        // gives the shortest dependency chain from the left pixel to the next pixel.
        // The first pixel has no left or upper-left neighbor. PNG uses 0 for both, so the loop starts with zero vectors.
        // After each pixel, its reconstructed bytes become the left neighbor, and its above bytes the upper-left neighbor.
        Vector128<short> left = Vector128<short>.Zero;
        Vector128<short> upperLeft = Vector128<short>.Zero;
        for (nuint x = 0; x < width; x++)
        {
            // The operator flags are constants after specialization, so a filter that does not use the preceding
            // scanline does not load it.
            Vector128<short> residual = Widen(TPixelOperator.Load(ref row, x));
            Vector128<short> above = TOperator.UsesAbove ? Widen(TPixelOperator.Load(ref previousRow, x)) : default;

            // A byte add wraps each low byte modulo 256, as PNG requires. Each high byte stays 0 + 0 = 0.
            Vector128<short> value = (residual.AsByte() + TOperator.Predict(left, above, upperLeft).AsByte()).AsInt16();

            // Narrow keeps the low byte of each lane. It is not on the dependency chain, because the next pixel reads
            // the widened value.
            TPixelOperator.Store(ref row, x, Vector128.Narrow(value.AsUInt16(), value.AsUInt16()));

            left = value;
            upperLeft = above;
        }
    }

    /// <summary>
    /// Zero-extends the 8 low byte lanes to 16-bit lanes.
    /// </summary>
    /// <param name="value">The bytes of one pixel.</param>
    /// <returns>The widened bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Widen(Vector128<byte> value) => Vector128.WidenLower(value).AsInt16();

    /// <summary>
    /// Reconstructs a scanline one byte at a time.
    /// </summary>
    /// <typeparam name="TOperator">The filter operator.</typeparam>
    /// <param name="scanline">The scanline, with the filter type byte first.</param>
    /// <param name="previousScanline">The reconstructed preceding scanline.</param>
    /// <param name="bytesPerPixel">The byte count of one pixel.</param>
    private static void DecodeScalar<TOperator>(Span<byte> scanline, ReadOnlySpan<byte> previousScanline, nuint bytesPerPixel)
        where TOperator : struct, IPngFilterOperator
    {
        ref byte scanRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousRef = ref MemoryMarshal.GetReference(previousScanline);
        nuint length = (uint)scanline.Length;
        nuint x = 1;

        // The bytes of the first pixel have no left or upper-left neighbor. PNG uses 0 for both.
        for (; x <= bytesPerPixel && x < length; x++)
        {
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousRef, x) : (byte)0;
            ref byte scan = ref Unsafe.Add(ref scanRef, x);
            scan = (byte)(scan + TOperator.Predict(0, above, 0));
        }

        for (; x < length; x++)
        {
            byte left = Unsafe.Add(ref scanRef, x - bytesPerPixel);
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousRef, x) : (byte)0;
            byte upperLeft = TOperator.UsesUpperLeft ? Unsafe.Add(ref previousRef, x - bytesPerPixel) : (byte)0;
            ref byte scan = ref Unsafe.Add(ref scanRef, x);
            scan = (byte)(scan + TOperator.Predict(left, above, upperLeft));
        }
    }
}
