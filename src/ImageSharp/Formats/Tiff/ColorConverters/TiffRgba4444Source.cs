// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads four interleaved four-bit RGBA components in TIFF most-significant-nibble order.
/// </summary>
internal readonly struct TiffRgba4444Source : ITiffNibbleColorSource
{
    // The first four byte indices select stored samples. The high bit zeros unused lanes in the native byte shuffle.
    private static readonly Vector128<byte> RgBytes = Vector128.Create((byte)0, 2, 4, 6, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
    private static readonly Vector128<byte> BaBytes = Vector128.Create((byte)1, 3, 5, 7, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);

    /// <inheritdoc />
    public int BitsPerPixel => 16;

    /// <inheritdoc />
    public int ComponentCount => 4;

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
        // Eight bytes hold four RGBA pixels, so no byte from the following row enters the register.
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(source0.Slice(index * 2)), 0UL).AsByte();
        Vector128<byte> rg = Vector128.ShuffleNative(bytes, RgBytes);
        Vector128<byte> ba = Vector128.ShuffleNative(bytes, BaBytes);
        Vector128<byte> red = Vector128.ShiftRightLogical(rg.AsUInt16(), 4).AsByte() & Vector128.Create((byte)15);
        Vector128<byte> green = rg & Vector128.Create((byte)15);
        Vector128<byte> blue = Vector128.ShiftRightLogical(ba.AsUInt16(), 4).AsByte() & Vector128.Create((byte)15);
        Vector128<byte> alpha = ba & Vector128.Create((byte)15);
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(red)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(green)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(blue)).AsUInt32());
        c3 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(alpha)).AsUInt32());
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
        // Each pixel occupies two bytes: [R G], then [B A], with the first component in the high nibble.
        int offset = index * 2;
        c0 = source0[offset] >> 4;
        c1 = source0[offset] & 15;
        c2 = source0[offset + 1] >> 4;
        c3 = source0[offset + 1] & 15;
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
        // Each word holds [R G] in its low byte and [B A] in its high byte.
        // Widen the complete group once, then extract all four nibbles in 256-bit lanes.
        Vector128<ushort> packed = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
        Vector256<uint> codes = Vector256.WidenLower(packed.ToVector256());
        Vector256<uint> mask = Vector256.Create(15U);
        c0 = Vector256.ConvertToSingle((codes >> 4) & mask);
        c1 = Vector256.ConvertToSingle(codes & mask);
        c2 = Vector256.ConvertToSingle((codes >> 12) & mask);
        c3 = Vector256.ConvertToSingle((codes >> 8) & mask);
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
        // Each word holds [R G] in its low byte and [B A] in its high byte.
        // Widen the complete group once, then extract all four nibbles in 512-bit lanes.
        Vector256<ushort> packed = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
        Vector512<uint> codes = Vector512.WidenLower(packed.ToVector512());
        Vector512<uint> mask = Vector512.Create(15U);
        c0 = Vector512.ConvertToSingle((codes >> 4) & mask);
        c1 = Vector512.ConvertToSingle(codes & mask);
        c2 = Vector512.ConvertToSingle((codes >> 12) & mask);
        c3 = Vector512.ConvertToSingle((codes >> 8) & mask);
    }
}
