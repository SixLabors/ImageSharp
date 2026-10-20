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
                Vector512<float> scaled = ColorNumerics.ClampToFinite(Vector512.LoadUnsafe(ref input, (uint)i), Vector512<float>.Zero, Vector512.Create(1F))
                    * Vector512.Create((float)ushort.MaxValue);

                Vector512<uint> codes = Vector512_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                Vector512.Narrow(codes, Vector512<uint>.Zero).GetLower().StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector256Count<float>();

            // Each float lane becomes one word. Scaling bounds every result to ushort range before rounding;
            // narrowing therefore retains all value bits. Only the lower half contains converted samples.
            for (nuint group = 0; group < groups; group++, i += Vector256<float>.Count)
            {
                Vector256<float> scaled = ColorNumerics.ClampToFinite(Vector256.LoadUnsafe(ref input, (uint)i), Vector256<float>.Zero, Vector256.Create(1F))
                    * Vector256.Create((float)ushort.MaxValue);

                Vector256<uint> codes = Vector256_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                Vector256.Narrow(codes, Vector256<uint>.Zero).GetLower().StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint groups = (source.Length - i).Vector128Count<float>();

            // Each float lane becomes one word. Scaling bounds every result to ushort range before rounding;
            // narrowing therefore retains all value bits. Only the lower half contains converted samples.
            for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
            {
                Vector128<float> scaled = ColorNumerics.ClampToFinite(Vector128.LoadUnsafe(ref input, (uint)i), Vector128<float>.Zero, Vector128.Create(1F))
                    * Vector128.Create((float)ushort.MaxValue);

                Vector128<uint> codes = Vector128_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                Vector128.Narrow(codes, Vector128<uint>.Zero).GetLower().StoreUnsafe(ref output, (uint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, (uint)i) = (ushort)MathF.Round(ColorNumerics.ClampToFinite(Unsafe.Add(ref input, (uint)i), 0F, 1F) * ushort.MaxValue);
        }
    }

    /// <summary>
    /// Converts unsigned sixteen-bit samples to floats in [0, 1] with scalar-equivalent division.
    /// </summary>
    /// <param name="source">The source component codes.</param>
    /// <param name="destination">The equally sized destination components.</param>
    public static void UInt16ToNormalizedFloat(ReadOnlySpan<ushort> source, Span<float> destination)
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
                (Vector512.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
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
                (Vector256.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
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
                (Vector128.ConvertToSingle(widened.AsInt32()) / maximum).StoreUnsafe(ref output, (uint)i);
            }
        }

        // Division matches the single-pixel API exactly; multiplying by a rounded reciprocal can change the final float bit.
        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, (uint)i) = Unsafe.Add(ref input, (uint)i) / (float)ushort.MaxValue;
        }
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
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.ByteToNormalizedFloatReduce(ref source, ref destination);

        if (source.Length > 0)
        {
            ConvertByteToNormalizedFloatRemainder(source, destination);
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
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.FloatToByteSaturateReduce(ref source, ref destination, byte.MaxValue);

        if (source.Length > 0)
        {
            ConvertFloatToByteRemainder(source, destination, byte.MaxValue);
        }
    }

    /// <summary>
    /// Converts byte-magnitude floating-point values to bytes using saturating round-to-nearest with midpoint values away from zero.
    /// </summary>
    /// <param name="source">The source byte magnitudes.</param>
    /// <param name="destination">The destination bytes.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void FloatToByteSaturate(ReadOnlySpan<float> source, Span<byte> destination)
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

        HwIntrinsics.FloatToByteSaturateReduce(ref source, ref destination, 1F);

        if (source.Length > 0)
        {
            ConvertFloatToByteRemainder(source, destination, 1F);
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
