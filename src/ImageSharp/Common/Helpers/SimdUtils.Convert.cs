// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Packs normalized components as unsigned sixteen-bit words, with saturation and midpoint-to-even rounding.
    /// </summary>
    /// <param name="source">The normalized components. All components use the same conversion, including a fourth ink channel.</param>
    /// <param name="destination">Storage for one word per source component.</param>
    public static void NormalizedFloatToUInt16Saturate(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref ushort output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector512Count<float>();

            // Each float lane becomes one word. Scaling bounds every result to ushort range before rounding;
            // narrowing therefore retains all value bits. Only the lower half contains converted samples.
            for (nuint group = 0; group < groups; group++, i += Vector512<float>.Count)
            {
                NormalizedFloatToUInt16Saturate(Vector512.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector256Count<float>();

            // Each float lane becomes one word. Scaling bounds every result to ushort range before rounding;
            // narrowing therefore retains all value bits. Only the lower half contains converted samples.
            for (nuint group = 0; group < groups; group++, i += Vector256<float>.Count)
            {
                NormalizedFloatToUInt16Saturate(Vector256.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector128Count<float>();

            // Each float lane becomes one word. Scaling bounds every result to ushort range before rounding;
            // narrowing therefore retains all value bits. Only the lower half contains converted samples.
            for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
            {
                NormalizedFloatToUInt16Saturate(Vector128.LoadUnsafe(ref input, (uint)i)).StoreUnsafe(ref output, (uint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, (uint)i) = (ushort)MathF.Round(ColorNumerics.ClampToFinite(Unsafe.Add(ref input, (uint)i), 0F, 1F) * ushort.MaxValue);
        }
    }

    /// <summary>
    /// Packs 4 normalized components into unsigned 16-bit codes with saturation and midpoint-to-even rounding.
    /// </summary>
    /// <param name="source">The independent normalized components.</param>
    /// <returns>The packed codes in source order, with one word per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector64<ushort> NormalizedFloatToUInt16Saturate(Vector128<float> source)
    {
        // Bound before multiplication, matching the scalar pixel API. NaN becomes zero and infinities select their bounds.
        Vector128<float> scaled = ColorNumerics.ClampToFinite(source, Vector128<float>.Zero, Vector128<float>.One)
            * Vector128.Create((float)ushort.MaxValue);

        // All rounded codes fit in ushort. Narrowing preserves their bits and order; only the lower half is output.
        Vector128<uint> codes = Vector128_.ConvertToInt32RoundToEven(scaled).AsUInt32();
        return Vector128.Narrow(codes, Vector128<uint>.Zero).GetLower();
    }

    /// <summary>
    /// Packs 8 normalized components into unsigned 16-bit codes with saturation and midpoint-to-even rounding.
    /// </summary>
    /// <param name="source">The independent normalized components.</param>
    /// <returns>The packed codes in source order, with one word per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> NormalizedFloatToUInt16Saturate(Vector256<float> source)
    {
        // Bound before multiplication, matching the scalar pixel API. NaN becomes zero and infinities select their bounds.
        Vector256<float> scaled = ColorNumerics.ClampToFinite(source, Vector256<float>.Zero, Vector256<float>.One)
            * Vector256.Create((float)ushort.MaxValue);

        // All rounded codes fit in ushort. Narrowing preserves their bits and order; only the lower half is output.
        Vector256<uint> codes = Vector256_.ConvertToInt32RoundToEven(scaled).AsUInt32();
        return Vector256.Narrow(codes, Vector256<uint>.Zero).GetLower();
    }

    /// <summary>
    /// Packs 16 normalized components into unsigned 16-bit codes with saturation and midpoint-to-even rounding.
    /// </summary>
    /// <param name="source">The independent normalized components.</param>
    /// <returns>The packed codes in source order, with one word per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<ushort> NormalizedFloatToUInt16Saturate(Vector512<float> source)
    {
        // Bound before multiplication, matching the scalar pixel API. NaN becomes zero and infinities select their bounds.
        Vector512<float> scaled = ColorNumerics.ClampToFinite(source, Vector512<float>.Zero, Vector512<float>.One)
            * Vector512.Create((float)ushort.MaxValue);

        // All rounded codes fit in ushort. Narrowing preserves their bits and order; only the lower half is output.
        Vector512<uint> codes = Vector512_.ConvertToInt32RoundToEven(scaled).AsUInt32();
        return Vector512.Narrow(codes, Vector512<uint>.Zero).GetLower();
    }

    /// <summary>
    /// Converts unsigned sixteen-bit samples to floats in [0, 1] with scalar-equivalent division.
    /// </summary>
    /// <param name="source">The source component codes.</param>
    /// <param name="destination">The equally sized destination components.</param>
    public static void UInt16ToNormalizedFloat(ReadOnlySpan<ushort> source, Span<float> destination)
        => UInt16ToNormalizedFloat<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination);

    /// <summary>
    /// Converts unsigned sixteen-bit RGBA samples to floats in [0, 1] with scalar-equivalent division, and applies
    /// <typeparamref name="TAlpha"/> to each pixel in the same pass. The spans hold whole RGBA pixels, unless
    /// <typeparamref name="TAlpha"/> is <see cref="PixelFormats.PixelOperations.NoAlphaOperator"/>.
    /// </summary>
    /// <remarks>
    /// Each float vector holds whole RGBA groups, because the vector widths are multiples of 4 floats and each step starts at
    /// a multiple of the vector width.
    /// </remarks>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="source">The source component codes.</param>
    /// <param name="destination">The equally sized destination components.</param>
    public static void UInt16ToNormalizedFloat<TAlpha>(ReadOnlySpan<ushort> source, Span<float> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        ref ushort input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Ushort codes fit in signed int lanes. Signed conversion avoids full-range uint emulation on older SIMD targets.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector512Count<float>();
            Vector512<float> maximum = Vector512.Create((float)ushort.MaxValue);

            // One 256-bit load supplies 16 ushort codes. Widen them once, then divide in 512-bit float lanes.
            for (nuint group = 0; group < groups; group++, i += Vector512<float>.Count)
            {
                Vector256<ushort> codes = Vector256.LoadUnsafe(ref input, (uint)i);
                Vector512<uint> widened = Vector512.WidenLower(codes.ToVector512());
                TAlpha.Apply(Vector512.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector256Count<float>();
            Vector256<float> maximum = Vector256.Create((float)ushort.MaxValue);

            // One 128-bit load supplies 8 ushort codes. Widen them once, then divide in 256-bit float lanes.
            for (nuint group = 0; group < groups; group++, i += Vector256<float>.Count)
            {
                Vector128<ushort> codes = Vector128.LoadUnsafe(ref input, (uint)i);
                Vector256<uint> widened = Vector256.WidenLower(codes.ToVector256());
                TAlpha.Apply(Vector256.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector128Count<float>();
            Vector128<float> maximum = Vector128.Create((float)ushort.MaxValue);

            // One 64-bit load supplies 4 ushort codes. Widen them once, then divide in 128-bit float lanes.
            for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
            {
                Vector64<ushort> codes = Vector64.LoadUnsafe(ref input, (uint)i);
                Vector128<uint> widened = Vector128.WidenLower(codes.ToVector128());
                TAlpha.Apply(Vector128.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
            }
        }

        // Division matches the single-pixel API exactly; multiplying by a rounded reciprocal can change the final float bit.
        int remainder = i;
        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, (uint)i) = Unsafe.Add(ref input, (uint)i) / (float)ushort.MaxValue;
        }

        ApplyAlphaToRemainder<TAlpha>(destination[remainder..source.Length]);
    }

    /// <summary>
    /// Converts all input <see cref="byte"/>-s to <see cref="float"/>-s normalized into [0..1].
    /// <paramref name="source"/> should be the of the same size as <paramref name="destination"/>,
    /// but there are no restrictions on the span's length.
    /// </summary>
    /// <param name="source">The source span of bytes</param>
    /// <param name="destination">The destination span of floats</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void ByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> destination)
        => ByteToNormalizedFloat<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination);

    /// <summary>
    /// Converts RGBA bytes to floats normalized into [0..1], and applies <typeparamref name="TAlpha"/> to each pixel in the
    /// same pass. The spans hold whole RGBA pixels, unless <typeparamref name="TAlpha"/> is <see cref="PixelFormats.PixelOperations.NoAlphaOperator"/>.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of floats.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void ByteToNormalizedFloat<TAlpha>(ReadOnlySpan<byte> source, Span<float> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.ByteToNormalizedFloatReduce<TAlpha>(ref source, ref destination);

        if (source.Length > 0)
        {
            ConvertByteToNormalizedFloatRemainder(source, destination);
            ApplyAlphaToRemainder<TAlpha>(destination);
        }
    }

    /// <summary>
    /// Convert all <see cref="float"/> values normalized into [0..1] from 'source' into 'destination' buffer of <see cref="byte"/>.
    /// The values are scaled up into [0-255] and rounded, overflows are clamped.
    /// <paramref name="source"/> should be the of the same size as <paramref name="destination"/>,
    /// but there are no restrictions on the span's length.
    /// </summary>
    /// <param name="source">The source span of floats</param>
    /// <param name="destination">The destination span of bytes</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void NormalizedFloatToByteSaturate(ReadOnlySpan<float> source, Span<byte> destination)
        => NormalizedFloatToByteSaturate<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination);

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to each RGBA pixel of normalized floats, and converts the floats to bytes in the
    /// same pass. The values are scaled up into [0-255] and rounded, overflows are clamped. The spans hold whole RGBA pixels,
    /// unless <typeparamref name="TAlpha"/> is <see cref="PixelFormats.PixelOperations.NoAlphaOperator"/>.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="source">The source span of floats.</param>
    /// <param name="destination">The destination span of bytes.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void NormalizedFloatToByteSaturate<TAlpha>(ReadOnlySpan<float> source, Span<byte> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.FloatToByteSaturateReduce<TAlpha>(ref source, ref destination, byte.MaxValue);

        if (source.Length > 0)
        {
            ConvertFloatToByteRemainder<TAlpha>(source, destination, byte.MaxValue);
        }
    }

    /// <summary>
    /// Converts byte-magnitude floating-point values to bytes using saturating round-to-nearest with midpoint values away from zero.
    /// </summary>
    /// <param name="source">The source byte magnitudes.</param>
    /// <param name="destination">The destination bytes.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void FloatToByteSaturate(ReadOnlySpan<float> source, Span<byte> destination)
        => FloatToByteSaturate<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination);

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to each RGBA pixel, and converts the resulting byte magnitudes to bytes in the same
    /// pass, with saturating round-to-nearest and midpoint values away from zero. The spans hold whole RGBA pixels, unless
    /// <typeparamref name="TAlpha"/> is <see cref="PixelFormats.PixelOperations.NoAlphaOperator"/>.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel. Its result is in byte magnitudes.</typeparam>
    /// <param name="source">The source components.</param>
    /// <param name="destination">The destination bytes.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void FloatToByteSaturate<TAlpha>(ReadOnlySpan<float> source, Span<byte> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.FloatToByteSaturateReduce<TAlpha>(ref source, ref destination, 1F);

        if (source.Length > 0)
        {
            ConvertFloatToByteRemainder<TAlpha>(source, destination, 1F);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvertByteToNormalizedFloatRemainder(ReadOnlySpan<byte> source, Span<float> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref float dBase = ref MemoryMarshal.GetReference(destination);

        for (int i = 0; i < source.Length; i++)
        {
            // Match the SIMD conversion so one span cannot contain different float representations of the same byte value.
            Unsafe.Add(ref dBase, (uint)i) = Unsafe.Add(ref sBase, (uint)i) / (float)byte.MaxValue;
        }
    }

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to each RGBA pixel of the remainder of a conversion to floats.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="destination">The converted floats. The length is a multiple of 4 for an alpha step other than straight.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyAlphaToRemainder<TAlpha>(Span<float> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        // NoAlphaOperator leaves the values unchanged, so the JIT removes this method body for it.
        if (typeof(TAlpha) == typeof(PixelFormats.PixelOperations.NoAlphaOperator))
        {
            return;
        }

        ref float dBase = ref MemoryMarshal.GetReference(destination);
        for (nuint i = 0; i < (uint)destination.Length; i += 4)
        {
            TAlpha.Apply(Vector128.LoadUnsafe(ref dBase, i)).StoreUnsafe(ref dBase, i);
        }
    }

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to each RGBA pixel of the remainder of a conversion to bytes, and converts it.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="source">The floats. The length is a multiple of 4 for an alpha step other than straight.</param>
    /// <param name="destination">The bytes.</param>
    /// <param name="scale">The factor applied before conversion.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvertFloatToByteRemainder<TAlpha>(ReadOnlySpan<float> source, Span<byte> destination, float scale)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        if (typeof(TAlpha) == typeof(PixelFormats.PixelOperations.NoAlphaOperator))
        {
            ConvertFloatToByteRemainder(source, destination, scale);
            return;
        }

        ref float sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);
        for (nuint i = 0; i < (uint)source.Length; i += 4)
        {
            // The alpha step runs on one pixel in a register. The source span stays unchanged.
            Vector128<float> pixel = TAlpha.Apply(Vector128.LoadUnsafe(ref sBase, i));
            Unsafe.Add(ref dBase, i) = ConvertToByte(pixel.GetElement(0), scale);
            Unsafe.Add(ref dBase, i + 1) = ConvertToByte(pixel.GetElement(1), scale);
            Unsafe.Add(ref dBase, i + 2) = ConvertToByte(pixel.GetElement(2), scale);
            Unsafe.Add(ref dBase, i + 3) = ConvertToByte(pixel.GetElement(3), scale);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvertFloatToByteRemainder(ReadOnlySpan<float> source, Span<byte> destination, float scale)
    {
        ref float sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        for (int i = 0; i < source.Length; i++)
        {
            Unsafe.Add(ref dBase, (uint)i) = ConvertToByte(Unsafe.Add(ref sBase, (uint)i), scale);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertToByte(float value, float scale) => (byte)ColorNumerics.ClampToFinite((value * scale) + .5F, 0, byte.MaxValue);
}
