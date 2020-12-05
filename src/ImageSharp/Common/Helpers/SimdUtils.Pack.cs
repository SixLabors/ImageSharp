using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        private static ReadOnlySpan<byte> ShuffleMaskShiftAlpha =>
            new byte[]
            {
                0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15,
                0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15
            };

        public static ReadOnlySpan<byte> PermuteMaskShiftAlpha8x32 => 
            new byte[]
            {
                0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0,
                5, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0
            };

        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void PackFromRgbPlanes(
            Configuration configuration,
            ReadOnlySpan<byte> redChannel,
            ReadOnlySpan<byte> greenChannel,
            ReadOnlySpan<byte> blueChannel,
            Span<Rgb24> destination)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                PackFromRgbPlanesAvx2Reduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
            }
            else
#endif
            {
                PackFromRgbPlanesScalarBatchedReduce(ref redChannel, ref greenChannel, ref blueChannel, ref destination);
            }

            PackFromRgbPlanesRemainder(redChannel, greenChannel, blueChannel, destination);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void PackFromRgbPlanes(
            Configuration configuration,
            ReadOnlySpan<byte> redChannel,
            ReadOnlySpan<byte> greenChannel,
            ReadOnlySpan<byte> blueChannel,
            Span<Rgba32> destination)
        {
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        internal static void PackFromRgbPlanesAvx2Reduce(
            ref ReadOnlySpan<byte> redChannel,
            ref ReadOnlySpan<byte> greenChannel,
            ref ReadOnlySpan<byte> blueChannel,
            ref Span<Rgb24> destination)
        {
            ref Vector256<byte> rBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(redChannel));
            ref Vector256<byte> gBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(greenChannel));
            ref Vector256<byte> bBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(blueChannel));
            ref byte dBase = ref Unsafe.As<Rgb24, byte>(ref MemoryMarshal.GetReference(destination));

            int count = redChannel.Length / Vector256<byte>.Count;

            ref byte control1Bytes = ref MemoryMarshal.GetReference(SimdUtils.HwIntrinsics.PermuteMaskEvenOdd8x32);
            Vector256<uint> control1 = Unsafe.As<byte, Vector256<uint>>(ref control1Bytes);

            ref byte control2Bytes = ref MemoryMarshal.GetReference(PermuteMaskShiftAlpha8x32);
            Vector256<uint> control2 = Unsafe.As<byte, Vector256<uint>>(ref control2Bytes);

            Vector256<byte> a = Vector256.Create((byte)255);

            Vector256<byte> shuffleAlpha = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(ShuffleMaskShiftAlpha));

            for (int i = 0; i < count; i++)
            {
                Vector256<byte> r0 = Unsafe.Add(ref rBase, i);
                Vector256<byte> g0 = Unsafe.Add(ref gBase, i);
                Vector256<byte> b0 = Unsafe.Add(ref bBase, i);

                r0 = Avx2.PermuteVar8x32(r0.AsUInt32(), control1).AsByte();
                g0 = Avx2.PermuteVar8x32(g0.AsUInt32(), control1).AsByte();
                b0 = Avx2.PermuteVar8x32(b0.AsUInt32(), control1).AsByte();

                Vector256<byte> rg = Avx2.UnpackLow(r0, g0);
                Vector256<byte> b1 = Avx2.UnpackLow(b0, a);

                Vector256<byte> rgb1 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                Vector256<byte> rgb2 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                rg = Avx2.UnpackHigh(r0, g0);
                b1 = Avx2.UnpackHigh(b0, a);

                Vector256<byte> rgb3 = Avx2.UnpackLow(rg.AsUInt16(), b1.AsUInt16()).AsByte();
                Vector256<byte> rgb4 = Avx2.UnpackHigh(rg.AsUInt16(), b1.AsUInt16()).AsByte();

                rgb1 = Avx2.Shuffle(rgb1, shuffleAlpha);
                rgb2 = Avx2.Shuffle(rgb2, shuffleAlpha);
                rgb3 = Avx2.Shuffle(rgb3, shuffleAlpha);
                rgb4 = Avx2.Shuffle(rgb4, shuffleAlpha);

                rgb1 = Avx2.PermuteVar8x32(rgb1.AsUInt32(), control2).AsByte();
                rgb2 = Avx2.PermuteVar8x32(rgb2.AsUInt32(), control2).AsByte();
                rgb3 = Avx2.PermuteVar8x32(rgb3.AsUInt32(), control2).AsByte();
                rgb4 = Avx2.PermuteVar8x32(rgb4.AsUInt32(), control2).AsByte();

                ref byte d1 = ref Unsafe.Add(ref dBase, 24 * 4 * i);
                ref byte d2 = ref Unsafe.Add(ref d1, 24);
                ref byte d3 = ref Unsafe.Add(ref d2, 24);
                ref byte d4 = ref Unsafe.Add(ref d3, 24);

                Unsafe.As<byte, Vector256<byte>>(ref d1) = rgb1;
                Unsafe.As<byte, Vector256<byte>>(ref d2) = rgb2;
                Unsafe.As<byte, Vector256<byte>>(ref d3) = rgb3;
                Unsafe.As<byte, Vector256<byte>>(ref d4) = rgb4;
            }

            int slice = count * Vector256<byte>.Count;
            redChannel = redChannel.Slice(slice);
            greenChannel = greenChannel.Slice(slice);
            blueChannel = blueChannel.Slice(slice);
            destination = destination.Slice(slice);
        }
#endif

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

            int count = destination.Length / 4;
            for (int i = 0; i < count; i++)
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

            int finished = count * 4;
            redChannel = redChannel.Slice(finished);
            greenChannel = greenChannel.Slice(finished);
            blueChannel = blueChannel.Slice(finished);
            destination = destination.Slice(finished);
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

            for (int i = 0; i < destination.Length; i++)
            {
                ref Rgb24 d = ref Unsafe.Add(ref rgb, i);
                d.R = Unsafe.Add(ref r, i);
                d.G = Unsafe.Add(ref g, i);
                d.B = Unsafe.Add(ref b, i);
            }
        }
    }
}