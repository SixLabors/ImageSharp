// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads three interleaved four-bit RGB components in TIFF most-significant-nibble order.
/// </summary>
internal readonly struct TiffRgb444Source : ITiffNibbleColorSource
{
    // The first four byte indices select stored samples. The high bit zeros unused lanes in the native byte shuffle.
    private static readonly Vector128<byte> RedBytes = Vector128.Create((byte)0, 1, 3, 6, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
    private static readonly Vector128<byte> GreenBytes = Vector128.Create((byte)0, 2, 3, 7, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
    private static readonly Vector128<byte> BlueBytes = Vector128.Create((byte)1, 2, 6, 7, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
    private static readonly Vector128<byte> EvenLanes = Vector128.Create((byte)255, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <inheritdoc />
    public int BitsPerPixel => 12;

    /// <inheritdoc />
    public int ComponentCount => 3;

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector128<float> c0,
        out Vector128<float> c1,
        out Vector128<float> c2,
        out Vector128<float> c3)
    {
        ReadOnlySpan<byte> packed = source0.Slice((index / 2) * 3);

        // Four RGB pixels occupy six bytes. Two overlapping four-byte reads keep the final group in bounds.
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<uint>(packed), MemoryMarshal.Read<uint>(packed.Slice(2)), 0U, 0U).AsByte();
        Vector128<byte> red = Vector128.ShuffleNative(bytes, RedBytes);
        Vector128<byte> green = Vector128.ShuffleNative(bytes, GreenBytes);
        Vector128<byte> blue = Vector128.ShuffleNative(bytes, BlueBytes);
        Vector128<byte> redCodes = Vector128.ConditionalSelect(
            EvenLanes,
            Vector128.ShiftRightLogical(red.AsUInt16(), 4).AsByte(),
            red) & Vector128.Create((byte)15);

        Vector128<byte> greenCodes = Vector128.ConditionalSelect(
            EvenLanes,
            green,
            Vector128.ShiftRightLogical(green.AsUInt16(), 4).AsByte()) & Vector128.Create((byte)15);

        Vector128<byte> blueCodes = Vector128.ConditionalSelect(
            EvenLanes,
            Vector128.ShiftRightLogical(blue.AsUInt16(), 4).AsByte(),
            blue) & Vector128.Create((byte)15);

        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(redCodes)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(greenCodes)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(blueCodes)).AsUInt32());
        c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out float c0,
        out float c1,
        out float c2,
        out float c3)
    {
        // An RGB444 pair occupies three bytes: [R0 G0], [B0 R1], [G1 B1].
        // An odd pixel starts in the low nibble of the middle byte.
        int offset = (index * 3) / 2;
        if ((index & 1) == 0)
        {
            c0 = source0[offset] >> 4;
            c1 = source0[offset] & 15;
            c2 = source0[offset + 1] >> 4;
        }
        else
        {
            c0 = source0[offset] & 15;
            c1 = source0[offset + 1] >> 4;
            c2 = source0[offset + 1] & 15;
        }

        c3 = 0F;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<float> c0,
        out Vector256<float> c1,
        out Vector256<float> c2,
        out Vector256<float> c3)
    {
        // Eight RGB444 pixels occupy twelve bytes. Pad the last four register bytes explicitly.
        ReadOnlySpan<byte> packed = source0.Slice((index / 2) * 3);
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(packed), (ulong)MemoryMarshal.Read<uint>(packed.Slice(8))).AsByte();
        Vector128<byte> red = Vector128.ShuffleNative(bytes, Vector128.Create((byte)0, 1, 3, 4, 6, 7, 9, 10, 255, 255, 255, 255, 255, 255, 255, 255));
        Vector128<byte> green = Vector128.ShuffleNative(bytes, Vector128.Create((byte)0, 2, 3, 5, 6, 8, 9, 11, 255, 255, 255, 255, 255, 255, 255, 255));
        Vector128<byte> blue = Vector128.ShuffleNative(bytes, Vector128.Create((byte)1, 2, 4, 5, 7, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255, 255));

        // Even pixels put R/B in high nibbles and G in low nibbles; odd pixels reverse that order.
        Vector128<byte> even = Vector128.Create((byte)255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0);
        Vector128<byte> mask = Vector128.Create((byte)15);
        red = Vector128.ConditionalSelect(even, (red.AsUInt16() >> 4).AsByte(), red) & mask;
        green = Vector128.ConditionalSelect(even, green, (green.AsUInt16() >> 4).AsByte()) & mask;
        blue = Vector128.ConditionalSelect(even, (blue.AsUInt16() >> 4).AsByte(), blue) & mask;

        c0 = Vector256.ConvertToSingle(Vector256_.Widen(red.GetLower()));
        c1 = Vector256.ConvertToSingle(Vector256_.Widen(green.GetLower()));
        c2 = Vector256.ConvertToSingle(Vector256_.Widen(blue.GetLower()));
        c3 = Vector256<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<float> c0,
        out Vector512<float> c1,
        out Vector512<float> c2,
        out Vector512<float> c3)
    {
        // Sixteen RGB444 pixels occupy twenty-four bytes. Overlapping loads at zero and eight stay inside the group.
        ref byte first = ref MemoryMarshal.GetReference(source0[(index / 2 * 3)..]);
        Vector128<byte> bytes = Vector128.LoadUnsafe(ref first);
        Vector128<byte> last = Vector128.LoadUnsafe(ref first, 8U);
        Vector128<byte> red = Vector128.ShuffleNative(bytes, Vector128.Create((byte)0, 1, 3, 4, 6, 7, 9, 10, 255, 255, 255, 255, 255, 255, 255, 255)) |
            Vector128.ShuffleNative(last, Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 4, 5, 7, 8, 10, 11, 13, 14));
        Vector128<byte> green = Vector128.ShuffleNative(bytes, Vector128.Create((byte)0, 2, 3, 5, 6, 8, 9, 11, 255, 255, 255, 255, 255, 255, 255, 255)) |
            Vector128.ShuffleNative(last, Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 4, 6, 7, 9, 10, 12, 13, 15));
        Vector128<byte> blue = Vector128.ShuffleNative(bytes, Vector128.Create((byte)1, 2, 4, 5, 7, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255, 255)) |
            Vector128.ShuffleNative(last, Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 5, 6, 8, 9, 11, 12, 14, 15));

        // Even pixels put R/B in high nibbles and G in low nibbles; odd pixels reverse that order.
        Vector128<byte> even = Vector128.Create((byte)255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0);
        Vector128<byte> mask = Vector128.Create((byte)15);
        red = Vector128.ConditionalSelect(even, (red.AsUInt16() >> 4).AsByte(), red) & mask;
        green = Vector128.ConditionalSelect(even, green, (green.AsUInt16() >> 4).AsByte()) & mask;
        blue = Vector128.ConditionalSelect(even, (blue.AsUInt16() >> 4).AsByte(), blue) & mask;

        c0 = Vector512.ConvertToSingle(Vector512_.Widen(red));
        c1 = Vector512.ConvertToSingle(Vector512_.Widen(green));
        c2 = Vector512.ConvertToSingle(Vector512_.Widen(blue));
        c3 = Vector512<float>.Zero;
    }
}
