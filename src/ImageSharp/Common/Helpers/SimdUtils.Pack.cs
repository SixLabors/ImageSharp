// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void PackFromRgbPlanes(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<Rgb24> destination)
    {
        DebugGuard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(destination.Length >= redChannel.Length, nameof(destination), "'destination' span should not be shorter than the source channels!");

        if (Avx2.IsSupported)
        {
            HwIntrinsics.PackFromRgbPlanesReduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            PackFromRgbPlanesVector128Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }
        else
        {
            PackFromRgbPlanesScalarBatchedReduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }

        PackFromRgbPlanesRemainder(redChannel, greenChannel, blueChannel, destination);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void PackFromRgbPlanes(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<Rgba32> destination)
    {
        DebugGuard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(destination.Length >= redChannel.Length, nameof(destination), "'destination' span should not be shorter than the source channels!");

        if (Avx2.IsSupported)
        {
            HwIntrinsics.PackFromRgbPlanesReduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            PackFromRgbPlanesVector128Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }
        else
        {
            PackFromRgbPlanesScalarBatchedReduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
        }

        PackFromRgbPlanesRemainder(redChannel, greenChannel, blueChannel, destination);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void UnpackToRgbPlanes(
        Span<float> redChannel,
        Span<float> greenChannel,
        Span<float> blueChannel,
        ReadOnlySpan<Rgb24> source)
    {
        DebugGuard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(source.Length <= redChannel.Length, nameof(source), "'source' span should not be bigger than the destination channels!");

        if (Avx2.IsSupported)
        {
            HwIntrinsics.UnpackToRgbPlanesReduce(ref redChannel, ref greenChannel, ref blueChannel, ref source);
        }

        UnpackToRgbPlanesScalar(redChannel, greenChannel, blueChannel, source);
    }

    /// <summary>
    /// Packs complete sixteen-pixel batches into exact-length <see cref="Rgb24"/> storage using portable 128-bit SIMD.
    /// </summary>
    /// <param name="redChannel">The red source span, advanced past the converted batches.</param>
    /// <param name="greenChannel">The green source span, advanced past the converted batches.</param>
    /// <param name="blueChannel">The blue source span, advanced past the converted batches.</param>
    /// <param name="destination">The destination span, advanced past the converted batches.</param>
    private static void PackFromRgbPlanesVector128Reduce(
        ref ReadOnlySpan<byte> redChannel,
        ref ReadOnlySpan<byte> greenChannel,
        ref ReadOnlySpan<byte> blueChannel,
        ref Span<Rgb24> destination)
    {
        ref byte redBase = ref MemoryMarshal.GetReference(redChannel);
        ref byte greenBase = ref MemoryMarshal.GetReference(greenChannel);
        ref byte blueBase = ref MemoryMarshal.GetReference(blueChannel);
        ref byte destinationBase = ref Unsafe.As<Rgb24, byte>(ref MemoryMarshal.GetReference(destination));
        Vector128<byte> opaqueAlpha = Vector128.Create(byte.MaxValue);
        Vector128<byte> removeAlpha = Vector128.Create((byte)0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        nuint batchCount = (nuint)(uint)redChannel.Length / (uint)Vector128<byte>.Count;

        for (nuint i = 0; i < batchCount; i++)
        {
            nuint sourceOffset = i * (uint)Vector128<byte>.Count;
            Vector128<byte> red = Vector128.LoadUnsafe(ref redBase, sourceOffset);
            Vector128<byte> green = Vector128.LoadUnsafe(ref greenBase, sourceOffset);
            Vector128<byte> blue = Vector128.LoadUnsafe(ref blueBase, sourceOffset);
            InterleaveRgbPlanes(red, green, blue, opaqueAlpha, out Vector128<byte> rgba0, out Vector128<byte> rgba1, out Vector128<byte> rgba2, out Vector128<byte> rgba3);

            // The native byte shuffle removes alpha from four pixels at a time. Each result owns twelve bytes, so
            // exact stores avoid coupling the SIMD path to padding beyond the row or the next memory-group segment.
            ref byte destination0 = ref Unsafe.Add(ref destinationBase, i * 48);
            StoreRgb24Batch(Vector128.ShuffleNative(rgba0, removeAlpha), ref destination0);
            StoreRgb24Batch(Vector128.ShuffleNative(rgba1, removeAlpha), ref Unsafe.Add(ref destination0, 12));
            StoreRgb24Batch(Vector128.ShuffleNative(rgba2, removeAlpha), ref Unsafe.Add(ref destination0, 24));
            StoreRgb24Batch(Vector128.ShuffleNative(rgba3, removeAlpha), ref Unsafe.Add(ref destination0, 36));
        }

        int convertedCount = (int)(batchCount * (uint)Vector128<byte>.Count);
        redChannel = redChannel[convertedCount..];
        greenChannel = greenChannel[convertedCount..];
        blueChannel = blueChannel[convertedCount..];
        destination = destination[convertedCount..];
    }

    /// <summary>
    /// Packs complete sixteen-pixel batches into exact-length <see cref="Rgba32"/> storage using portable 128-bit SIMD.
    /// </summary>
    /// <param name="redChannel">The red source span, advanced past the converted batches.</param>
    /// <param name="greenChannel">The green source span, advanced past the converted batches.</param>
    /// <param name="blueChannel">The blue source span, advanced past the converted batches.</param>
    /// <param name="destination">The destination span, advanced past the converted batches.</param>
    private static void PackFromRgbPlanesVector128Reduce(
        ref ReadOnlySpan<byte> redChannel,
        ref ReadOnlySpan<byte> greenChannel,
        ref ReadOnlySpan<byte> blueChannel,
        ref Span<Rgba32> destination)
    {
        ref byte redBase = ref MemoryMarshal.GetReference(redChannel);
        ref byte greenBase = ref MemoryMarshal.GetReference(greenChannel);
        ref byte blueBase = ref MemoryMarshal.GetReference(blueChannel);
        ref Vector128<byte> destinationBase = ref Unsafe.As<Rgba32, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));
        Vector128<byte> opaqueAlpha = Vector128.Create(byte.MaxValue);
        nuint batchCount = (nuint)(uint)redChannel.Length / (uint)Vector128<byte>.Count;

        for (nuint i = 0; i < batchCount; i++)
        {
            nuint sourceOffset = i * (uint)Vector128<byte>.Count;
            Vector128<byte> red = Vector128.LoadUnsafe(ref redBase, sourceOffset);
            Vector128<byte> green = Vector128.LoadUnsafe(ref greenBase, sourceOffset);
            Vector128<byte> blue = Vector128.LoadUnsafe(ref blueBase, sourceOffset);
            InterleaveRgbPlanes(red, green, blue, opaqueAlpha, out Vector128<byte> rgba0, out Vector128<byte> rgba1, out Vector128<byte> rgba2, out Vector128<byte> rgba3);

            ref Vector128<byte> destination0 = ref Unsafe.Add(ref destinationBase, i * 4);
            destination0 = rgba0;
            Unsafe.Add(ref destination0, 1) = rgba1;
            Unsafe.Add(ref destination0, 2) = rgba2;
            Unsafe.Add(ref destination0, 3) = rgba3;
        }

        int convertedCount = (int)(batchCount * (uint)Vector128<byte>.Count);
        redChannel = redChannel[convertedCount..];
        greenChannel = greenChannel[convertedCount..];
        blueChannel = blueChannel[convertedCount..];
        destination = destination[convertedCount..];
    }

    /// <summary>
    /// Interleaves sixteen planar RGB samples into four groups of four opaque RGBA pixels.
    /// </summary>
    /// <param name="red">The red component lanes.</param>
    /// <param name="green">The green component lanes.</param>
    /// <param name="blue">The blue component lanes.</param>
    /// <param name="alpha">The opaque alpha lanes.</param>
    /// <param name="rgba0">The first four interleaved pixels.</param>
    /// <param name="rgba1">The second four interleaved pixels.</param>
    /// <param name="rgba2">The third four interleaved pixels.</param>
    /// <param name="rgba3">The fourth four interleaved pixels.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterleaveRgbPlanes(
        Vector128<byte> red,
        Vector128<byte> green,
        Vector128<byte> blue,
        Vector128<byte> alpha,
        out Vector128<byte> rgba0,
        out Vector128<byte> rgba1,
        out Vector128<byte> rgba2,
        out Vector128<byte> rgba3)
    {
        Vector128<byte> redGreenLow = Vector128_.UnpackLow(red, green);
        Vector128<byte> redGreenHigh = Vector128_.UnpackHigh(red, green);
        Vector128<byte> blueAlphaLow = Vector128_.UnpackLow(blue, alpha);
        Vector128<byte> blueAlphaHigh = Vector128_.UnpackHigh(blue, alpha);
        rgba0 = Vector128_.UnpackLow(redGreenLow.AsInt16(), blueAlphaLow.AsInt16()).AsByte();
        rgba1 = Vector128_.UnpackHigh(redGreenLow.AsInt16(), blueAlphaLow.AsInt16()).AsByte();
        rgba2 = Vector128_.UnpackLow(redGreenHigh.AsInt16(), blueAlphaHigh.AsInt16()).AsByte();
        rgba3 = Vector128_.UnpackHigh(redGreenHigh.AsInt16(), blueAlphaHigh.AsInt16()).AsByte();
    }

    /// <summary>
    /// Stores the twelve packed RGB bytes in one shuffled SIMD value without writing its unused lanes.
    /// </summary>
    /// <param name="value">The packed RGB bytes in the first twelve lanes.</param>
    /// <param name="destination">The first destination byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRgb24Batch(Vector128<byte> value, ref byte destination)
    {
        Unsafe.WriteUnaligned(ref destination, value.AsUInt64().ToScalar());
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, 8), value.AsUInt32().GetElement(2));
    }

    private static void PackFromRgbPlanesScalarBatchedReduce(
        ref ReadOnlySpan<byte> redChannel,
        ref ReadOnlySpan<byte> greenChannel,
        ref ReadOnlySpan<byte> blueChannel,
        ref Span<Rgb24> destination)
    {
        ref ByteTuple4 r = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(redChannel));
        ref ByteTuple4 g = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(greenChannel));
        ref ByteTuple4 b = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(blueChannel));
        ref Rgb24 rgb = ref MemoryMarshal.GetReference(destination);

        nuint batchCount = (uint)redChannel.Length / 4;
        for (nuint i = 0; i < batchCount; i++)
        {
            ref Rgb24 d0 = ref Unsafe.Add(ref rgb, i * 4);
            ref Rgb24 d1 = ref Unsafe.Add(ref d0, 1);
            ref Rgb24 d2 = ref Unsafe.Add(ref d0, 2);
            ref Rgb24 d3 = ref Unsafe.Add(ref d0, 3);

            ref ByteTuple4 rr = ref Unsafe.Add(ref r, i);
            ref ByteTuple4 gg = ref Unsafe.Add(ref g, i);
            ref ByteTuple4 bb = ref Unsafe.Add(ref b, i);

            d0.R = rr.V0;
            d0.G = gg.V0;
            d0.B = bb.V0;

            d1.R = rr.V1;
            d1.G = gg.V1;
            d1.B = bb.V1;

            d2.R = rr.V2;
            d2.G = gg.V2;
            d2.B = bb.V2;

            d3.R = rr.V3;
            d3.G = gg.V3;
            d3.B = bb.V3;
        }

        int convertedCount = (int)(batchCount * 4);
        redChannel = redChannel[convertedCount..];
        greenChannel = greenChannel[convertedCount..];
        blueChannel = blueChannel[convertedCount..];
        destination = destination[convertedCount..];
    }

    private static void PackFromRgbPlanesScalarBatchedReduce(
        ref ReadOnlySpan<byte> redChannel,
        ref ReadOnlySpan<byte> greenChannel,
        ref ReadOnlySpan<byte> blueChannel,
        ref Span<Rgba32> destination)
    {
        ref ByteTuple4 r = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(redChannel));
        ref ByteTuple4 g = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(greenChannel));
        ref ByteTuple4 b = ref Unsafe.As<byte, ByteTuple4>(ref MemoryMarshal.GetReference(blueChannel));
        ref Rgba32 rgb = ref MemoryMarshal.GetReference(destination);

        nuint batchCount = (uint)redChannel.Length / 4;
        destination.Fill(new Rgba32(0, 0, 0, 255));
        for (nuint i = 0; i < batchCount; i++)
        {
            ref Rgba32 d0 = ref Unsafe.Add(ref rgb, i * 4);
            ref Rgba32 d1 = ref Unsafe.Add(ref d0, 1);
            ref Rgba32 d2 = ref Unsafe.Add(ref d0, 2);
            ref Rgba32 d3 = ref Unsafe.Add(ref d0, 3);

            ref ByteTuple4 rr = ref Unsafe.Add(ref r, i);
            ref ByteTuple4 gg = ref Unsafe.Add(ref g, i);
            ref ByteTuple4 bb = ref Unsafe.Add(ref b, i);

            d0.R = rr.V0;
            d0.G = gg.V0;
            d0.B = bb.V0;

            d1.R = rr.V1;
            d1.G = gg.V1;
            d1.B = bb.V1;

            d2.R = rr.V2;
            d2.G = gg.V2;
            d2.B = bb.V2;

            d3.R = rr.V3;
            d3.G = gg.V3;
            d3.B = bb.V3;
        }

        int convertedCount = (int)(batchCount * 4);
        redChannel = redChannel[convertedCount..];
        greenChannel = greenChannel[convertedCount..];
        blueChannel = blueChannel[convertedCount..];
        destination = destination[convertedCount..];
    }

    private static void PackFromRgbPlanesRemainder(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<Rgb24> destination)
    {
        ref byte r = ref MemoryMarshal.GetReference(redChannel);
        ref byte g = ref MemoryMarshal.GetReference(greenChannel);
        ref byte b = ref MemoryMarshal.GetReference(blueChannel);
        ref Rgb24 rgb = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)redChannel.Length; i++)
        {
            ref Rgb24 d = ref Unsafe.Add(ref rgb, i);
            d.R = Unsafe.Add(ref r, i);
            d.G = Unsafe.Add(ref g, i);
            d.B = Unsafe.Add(ref b, i);
        }
    }

    private static void PackFromRgbPlanesRemainder(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<Rgba32> destination)
    {
        ref byte r = ref MemoryMarshal.GetReference(redChannel);
        ref byte g = ref MemoryMarshal.GetReference(greenChannel);
        ref byte b = ref MemoryMarshal.GetReference(blueChannel);
        ref Rgba32 rgba = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)redChannel.Length; i++)
        {
            ref Rgba32 d = ref Unsafe.Add(ref rgba, i);
            d.R = Unsafe.Add(ref r, i);
            d.G = Unsafe.Add(ref g, i);
            d.B = Unsafe.Add(ref b, i);
            d.A = 255;
        }
    }

    private static void UnpackToRgbPlanesScalar(
        Span<float> redChannel,
        Span<float> greenChannel,
        Span<float> blueChannel,
        ReadOnlySpan<Rgb24> source)
    {
        DebugGuard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        DebugGuard.IsTrue(source.Length <= redChannel.Length, nameof(source), "'source' span should not be bigger than the destination channels!");

        ref float r = ref MemoryMarshal.GetReference(redChannel);
        ref float g = ref MemoryMarshal.GetReference(greenChannel);
        ref float b = ref MemoryMarshal.GetReference(blueChannel);
        ref Rgb24 rgb = ref MemoryMarshal.GetReference(source);

        for (nuint i = 0; i < (uint)source.Length; i++)
        {
            ref Rgb24 src = ref Unsafe.Add(ref rgb, i);
            Unsafe.Add(ref r, i) = src.R;
            Unsafe.Add(ref g, i) = src.G;
            Unsafe.Add(ref b, i) = src.B;
        }
    }

    /// <summary>
    /// Provides the hardware-intrinsic reducers used by the planar RGB packing pipeline.
    /// </summary>
    public static partial class HwIntrinsics
    {
        /// <summary>
        /// Creates the AVX2 lane order used before interleaving planar RGB components.
        /// </summary>
        /// <returns>The source lane permutation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> PermuteMaskEvenOdd8x32() => Vector256.Create(0u, 2, 4, 6, 1, 3, 5, 7);

        /// <summary>
        /// Packs complete AVX2 batches into <see cref="Rgb24"/> pixels and retains the unconverted remainder.
        /// </summary>
        /// <param name="redChannel">The red source span.</param>
        /// <param name="greenChannel">The green source span.</param>
        /// <param name="blueChannel">The blue source span.</param>
        /// <param name="destination">The destination pixel span.</param>
        internal static void PackFromRgbPlanesReduce(
            ref ReadOnlySpan<byte> redChannel,
            ref ReadOnlySpan<byte> greenChannel,
            ref ReadOnlySpan<byte> blueChannel,
            ref Span<Rgb24> destination)
        {
            ref Vector256<byte> redBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(redChannel));
            ref Vector256<byte> greenBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(greenChannel));
            ref Vector256<byte> blueBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(blueChannel));
            ref byte destinationBase = ref Unsafe.As<Rgb24, byte>(ref MemoryMarshal.GetReference(destination));
            nuint batchCount = redChannel.Vector256Count<byte>();
            Vector256<uint> sourceOrder = PermuteMaskEvenOdd8x32();
            Vector256<uint> packedOrder = Vector256.Create(0u, 1, 2, 4, 5, 6, 3, 7);
            Vector256<byte> opaqueAlpha = Vector256.Create(byte.MaxValue);
            Vector128<byte> removeAlphaLower = Vector128.Create((byte)0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15);
            Vector128<byte> removeAlphaUpper = Vector128.Create((byte)16, 17, 18, 20, 21, 22, 24, 25, 26, 28, 29, 30, 19, 23, 27, 31);
            Vector256<byte> removeAlpha = Vector256.Create(removeAlphaLower, removeAlphaUpper);

            bool hasWritablePadding = destination.Length >= redChannel.Length + 3;
            nuint i = 0;

            // Non-final batches retain the original four overlapping wide stores. Splitting the final batch keeps
            // the exact-row decision out of the hot loop and limits the narrower stores to the only bytes that can
            // cross the destination boundary.
            for (; i + 1 < batchCount; i++)
            {
                PackRgb24Batch(
                    Unsafe.Add(ref redBase, i),
                    Unsafe.Add(ref greenBase, i),
                    Unsafe.Add(ref blueBase, i),
                    opaqueAlpha,
                    sourceOrder,
                    packedOrder,
                    removeAlpha,
                    out Vector256<byte> rgb0,
                    out Vector256<byte> rgb1,
                    out Vector256<byte> rgb2,
                    out Vector256<byte> rgb3);

                ref byte destination0 = ref Unsafe.Add(ref destinationBase, 96 * i);
                ref byte destination1 = ref Unsafe.Add(ref destination0, 24);
                ref byte destination2 = ref Unsafe.Add(ref destination1, 24);
                ref byte destination3 = ref Unsafe.Add(ref destination2, 24);

                Unsafe.As<byte, Vector256<byte>>(ref destination0) = rgb0;
                Unsafe.As<byte, Vector256<byte>>(ref destination1) = rgb1;
                Unsafe.As<byte, Vector256<byte>>(ref destination2) = rgb2;
                Unsafe.As<byte, Vector256<byte>>(ref destination3) = rgb3;
            }

            if (i < batchCount)
            {
                PackRgb24Batch(
                    Unsafe.Add(ref redBase, i),
                    Unsafe.Add(ref greenBase, i),
                    Unsafe.Add(ref blueBase, i),
                    opaqueAlpha,
                    sourceOrder,
                    packedOrder,
                    removeAlpha,
                    out Vector256<byte> rgb0,
                    out Vector256<byte> rgb1,
                    out Vector256<byte> rgb2,
                    out Vector256<byte> rgb3);

                ref byte destination0 = ref Unsafe.Add(ref destinationBase, 96 * i);
                ref byte destination1 = ref Unsafe.Add(ref destination0, 24);
                ref byte destination2 = ref Unsafe.Add(ref destination1, 24);
                ref byte destination3 = ref Unsafe.Add(ref destination2, 24);

                Unsafe.As<byte, Vector256<byte>>(ref destination0) = rgb0;
                Unsafe.As<byte, Vector256<byte>>(ref destination1) = rgb1;
                Unsafe.As<byte, Vector256<byte>>(ref destination2) = rgb2;

                if (hasWritablePadding)
                {
                    Unsafe.As<byte, Vector256<byte>>(ref destination3) = rgb3;
                }
                else
                {
                    // The final compacted vector contains 24 RGB bytes followed by eight unused bytes. Exact stores
                    // retain all useful bytes without writing beyond an unpadded destination row.
                    Unsafe.As<byte, Vector128<byte>>(ref destination3) = rgb3.GetLower();
                    Unsafe.As<byte, ulong>(ref Unsafe.Add(ref destination3, 16)) = rgb3.GetUpper().AsUInt64().ToScalar();
                }
            }

            int convertedCount = (int)batchCount * Vector256<byte>.Count;
            redChannel = redChannel[convertedCount..];
            greenChannel = greenChannel[convertedCount..];
            blueChannel = blueChannel[convertedCount..];
            destination = destination[convertedCount..];
        }

        /// <summary>
        /// Packs complete AVX2 batches into <see cref="Rgba32"/> pixels and retains the unconverted remainder.
        /// </summary>
        /// <param name="redChannel">The red source span.</param>
        /// <param name="greenChannel">The green source span.</param>
        /// <param name="blueChannel">The blue source span.</param>
        /// <param name="destination">The destination pixel span.</param>
        internal static void PackFromRgbPlanesReduce(
            ref ReadOnlySpan<byte> redChannel,
            ref ReadOnlySpan<byte> greenChannel,
            ref ReadOnlySpan<byte> blueChannel,
            ref Span<Rgba32> destination)
        {
            ref Vector256<byte> redBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(redChannel));
            ref Vector256<byte> greenBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(greenChannel));
            ref Vector256<byte> blueBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(blueChannel));
            ref Vector256<byte> destinationBase = ref Unsafe.As<Rgba32, Vector256<byte>>(ref MemoryMarshal.GetReference(destination));
            nuint batchCount = redChannel.Vector256Count<byte>();
            Vector256<uint> sourceOrder = PermuteMaskEvenOdd8x32();
            Vector256<byte> opaqueAlpha = Vector256.Create(byte.MaxValue);

            for (nuint i = 0; i < batchCount; i++)
            {
                InterleaveRgbPlanes(
                    Unsafe.Add(ref redBase, i),
                    Unsafe.Add(ref greenBase, i),
                    Unsafe.Add(ref blueBase, i),
                    opaqueAlpha,
                    sourceOrder,
                    out Vector256<byte> rgba0,
                    out Vector256<byte> rgba1,
                    out Vector256<byte> rgba2,
                    out Vector256<byte> rgba3);

                ref Vector256<byte> destination0 = ref Unsafe.Add(ref destinationBase, i * 4);
                destination0 = rgba0;
                Unsafe.Add(ref destination0, 1) = rgba1;
                Unsafe.Add(ref destination0, 2) = rgba2;
                Unsafe.Add(ref destination0, 3) = rgba3;
            }

            int convertedCount = (int)batchCount * Vector256<byte>.Count;
            redChannel = redChannel[convertedCount..];
            greenChannel = greenChannel[convertedCount..];
            blueChannel = blueChannel[convertedCount..];
            destination = destination[convertedCount..];
        }

        /// <summary>
        /// Unpacks complete AVX2 batches from <see cref="Rgb24"/> pixels and retains the unconverted remainder.
        /// </summary>
        /// <param name="redChannel">The red destination span.</param>
        /// <param name="greenChannel">The green destination span.</param>
        /// <param name="blueChannel">The blue destination span.</param>
        /// <param name="source">The source pixel span.</param>
        internal static void UnpackToRgbPlanesReduce(
            ref Span<float> redChannel,
            ref Span<float> greenChannel,
            ref Span<float> blueChannel,
            ref ReadOnlySpan<Rgb24> source)
        {
            ref Vector256<byte> sourceBase = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(source));
            ref Vector256<float> redBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(redChannel));
            ref Vector256<float> greenBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(greenChannel));
            ref Vector256<float> blueBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(blueChannel));
            Vector256<uint> separateLanes = Vector256.Create(0u, 1, 2, 6, 3, 4, 5, 7);
            Vector128<byte> extractRgbLower = Vector128.Create((byte)0, 3, 6, 9, 1, 4, 7, 10, 2, 5, 8, 11, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            Vector128<byte> extractRgbUpper = Vector128.Create((byte)16, 19, 22, 25, 17, 20, 23, 26, 18, 21, 24, 27, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            Vector256<byte> extractRgb = Vector256.Create(extractRgbLower, extractRgbUpper);

            // Each iteration consumes eight Rgb24 pixels, or 24 bytes, but starts with a 32-byte load. Three extra
            // source pixels must therefore remain addressable beyond every vectorized batch.
            const int bytesPerBatch = 24;
            nuint batchCount = source.Length > 3 ? (uint)(source.Length - 3) / 8 : 0;

            for (nuint i = 0; i < batchCount; i++)
            {
                Vector256<byte> packed = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref sourceBase, (uint)(bytesPerBatch * i)).AsUInt32(), separateLanes).AsByte();
                packed = Vector256.ShuffleNative(packed, extractRgb);

                Vector256<byte> redGreen = Avx2.UnpackLow(packed, Vector256<byte>.Zero);
                Vector256<byte> blue = Avx2.UnpackHigh(packed, Vector256<byte>.Zero);
                Vector256<float> red = Avx.ConvertToVector256Single(Avx2.UnpackLow(redGreen, Vector256<byte>.Zero).AsInt32());
                Vector256<float> green = Avx.ConvertToVector256Single(Avx2.UnpackHigh(redGreen, Vector256<byte>.Zero).AsInt32());
                Vector256<float> blueValues = Avx.ConvertToVector256Single(Avx2.UnpackLow(blue, Vector256<byte>.Zero).AsInt32());

                Unsafe.Add(ref redBase, i) = red;
                Unsafe.Add(ref greenBase, i) = green;
                Unsafe.Add(ref blueBase, i) = blueValues;
            }

            int convertedCount = (int)(batchCount * 8);
            redChannel = redChannel[convertedCount..];
            greenChannel = greenChannel[convertedCount..];
            blueChannel = blueChannel[convertedCount..];
            source = source[convertedCount..];
        }

        /// <summary>
        /// Interleaves and compacts one AVX2 batch into four groups of eight <see cref="Rgb24"/> pixels.
        /// </summary>
        /// <param name="red">The red component lanes.</param>
        /// <param name="green">The green component lanes.</param>
        /// <param name="blue">The blue component lanes.</param>
        /// <param name="alpha">The opaque alpha lanes used during interleaving.</param>
        /// <param name="sourceOrder">The cross-lane source permutation.</param>
        /// <param name="packedOrder">The cross-lane packed RGB permutation.</param>
        /// <param name="removeAlpha">The native byte-shuffle indices that compact RGBA to RGB.</param>
        /// <param name="rgb0">The first eight packed pixels.</param>
        /// <param name="rgb1">The second eight packed pixels.</param>
        /// <param name="rgb2">The third eight packed pixels.</param>
        /// <param name="rgb3">The fourth eight packed pixels.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PackRgb24Batch(
            Vector256<byte> red,
            Vector256<byte> green,
            Vector256<byte> blue,
            Vector256<byte> alpha,
            Vector256<uint> sourceOrder,
            Vector256<uint> packedOrder,
            Vector256<byte> removeAlpha,
            out Vector256<byte> rgb0,
            out Vector256<byte> rgb1,
            out Vector256<byte> rgb2,
            out Vector256<byte> rgb3)
        {
            InterleaveRgbPlanes(red, green, blue, alpha, sourceOrder, out Vector256<byte> rgba0, out Vector256<byte> rgba1, out Vector256<byte> rgba2, out Vector256<byte> rgba3);

            rgb0 = Avx2.PermuteVar8x32(Vector256.ShuffleNative(rgba0, removeAlpha).AsUInt32(), packedOrder).AsByte();
            rgb1 = Avx2.PermuteVar8x32(Vector256.ShuffleNative(rgba1, removeAlpha).AsUInt32(), packedOrder).AsByte();
            rgb2 = Avx2.PermuteVar8x32(Vector256.ShuffleNative(rgba2, removeAlpha).AsUInt32(), packedOrder).AsByte();
            rgb3 = Avx2.PermuteVar8x32(Vector256.ShuffleNative(rgba3, removeAlpha).AsUInt32(), packedOrder).AsByte();
        }

        /// <summary>
        /// Interleaves 32 planar RGB samples into four groups of eight opaque RGBA pixels.
        /// </summary>
        /// <param name="red">The red component lanes.</param>
        /// <param name="green">The green component lanes.</param>
        /// <param name="blue">The blue component lanes.</param>
        /// <param name="alpha">The opaque alpha lanes.</param>
        /// <param name="sourceOrder">The cross-lane source permutation.</param>
        /// <param name="rgba0">The first eight interleaved pixels.</param>
        /// <param name="rgba1">The second eight interleaved pixels.</param>
        /// <param name="rgba2">The third eight interleaved pixels.</param>
        /// <param name="rgba3">The fourth eight interleaved pixels.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InterleaveRgbPlanes(
            Vector256<byte> red,
            Vector256<byte> green,
            Vector256<byte> blue,
            Vector256<byte> alpha,
            Vector256<uint> sourceOrder,
            out Vector256<byte> rgba0,
            out Vector256<byte> rgba1,
            out Vector256<byte> rgba2,
            out Vector256<byte> rgba3)
        {
            red = Avx2.PermuteVar8x32(red.AsUInt32(), sourceOrder).AsByte();
            green = Avx2.PermuteVar8x32(green.AsUInt32(), sourceOrder).AsByte();
            blue = Avx2.PermuteVar8x32(blue.AsUInt32(), sourceOrder).AsByte();

            Vector256<byte> redGreenLow = Avx2.UnpackLow(red, green);
            Vector256<byte> redGreenHigh = Avx2.UnpackHigh(red, green);
            Vector256<byte> blueAlphaLow = Avx2.UnpackLow(blue, alpha);
            Vector256<byte> blueAlphaHigh = Avx2.UnpackHigh(blue, alpha);

            rgba0 = Avx2.UnpackLow(redGreenLow.AsUInt16(), blueAlphaLow.AsUInt16()).AsByte();
            rgba1 = Avx2.UnpackHigh(redGreenLow.AsUInt16(), blueAlphaLow.AsUInt16()).AsByte();
            rgba2 = Avx2.UnpackLow(redGreenHigh.AsUInt16(), blueAlphaHigh.AsUInt16()).AsByte();
            rgba3 = Avx2.UnpackHigh(redGreenHigh.AsUInt16(), blueAlphaHigh.AsUInt16()).AsByte();
        }
    }
}
