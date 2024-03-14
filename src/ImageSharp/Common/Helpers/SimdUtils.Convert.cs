// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
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

        HwIntrinsics.NormalizedFloatToByteSaturateReduce(ref source, ref destination);

        if (source.Length > 0)
        {
            ConvertNormalizedFloatToByteRemainder(source, destination);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvertByteToNormalizedFloatRemainder(ReadOnlySpan<byte> source, Span<float> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref float dBase = ref MemoryMarshal.GetReference(destination);

        for (int i = 0; i < source.Length; i++)
        {
            Unsafe.Add(ref dBase, (uint)i) = Unsafe.Add(ref sBase, (uint)i) / 255f;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConvertNormalizedFloatToByteRemainder(ReadOnlySpan<float> source, Span<byte> destination)
    {
        ref float sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        for (int i = 0; i < source.Length; i++)
        {
            Unsafe.Add(ref dBase, (uint)i) = ConvertToByte(Unsafe.Add(ref sBase, (uint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertToByte(float f) => (byte)Numerics.Clamp((f * 255f) + 0.5f, 0, 255f);
}
