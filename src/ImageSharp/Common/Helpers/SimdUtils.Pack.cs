// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
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
        DebugGuard.IsTrue(destination.Length > redChannel.Length + 2, nameof(destination), "'destination' must contain a padding of 3 elements!");

        if (Avx2.IsSupported)
        {
            HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
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
        DebugGuard.IsTrue(destination.Length > redChannel.Length, nameof(destination), "'destination' span should not be shorter than the source channels!");

        if (Avx2.IsSupported)
        {
            HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
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
            HwIntrinsics.UnpackToRgbPlanesAvx2Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref source);
        }

        UnpackToRgbPlanesScalar(redChannel, greenChannel, blueChannel, source);
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

        nuint count = (uint)redChannel.Length / 4;
        for (nuint i = 0; i < count; i++)
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

        int finished = (int)(count * 4);
        redChannel = redChannel[finished..];
        greenChannel = greenChannel[finished..];
        blueChannel = blueChannel[finished..];
        destination = destination[finished..];
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

        nuint count = (uint)redChannel.Length / 4;
        destination.Fill(new(0, 0, 0, 255));
        for (nuint i = 0; i < count; i++)
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

        int finished = (int)(count * 4);
        redChannel = redChannel[finished..];
        greenChannel = greenChannel[finished..];
        blueChannel = blueChannel[finished..];
        destination = destination[finished..];
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

        for (nuint i = 0; i < (uint)destination.Length; i++)
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

        for (nuint i = 0; i < (uint)destination.Length; i++)
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
}
