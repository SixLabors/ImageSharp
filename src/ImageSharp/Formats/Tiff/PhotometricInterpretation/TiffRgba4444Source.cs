// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four interleaved four-bit RGBA components in TIFF most-significant-nibble order.
/// </summary>
internal readonly struct TiffRgba4444Source : ITiffNibbleColorSource
{
    private static readonly Vector128<byte> RgBytes = Vector128.Create((byte)0, 2, 4, 6, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
    private static readonly Vector128<byte> BaBytes = Vector128.Create((byte)1, 3, 5, 7, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);

    /// <inheritdoc />
    public int BitsPerPixel => 16;

    /// <inheritdoc />
    public int ComponentCount => 4;

    /// <inheritdoc />
    public void ReadFour(ReadOnlySpan<byte> row, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        // Eight bytes hold four RGBA pixels, so no byte from the following row enters the register.
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(row.Slice(index * 2)), 0UL).AsByte();
        Vector128<byte> rg = Vector128.Shuffle(bytes, RgBytes);
        Vector128<byte> ba = Vector128.Shuffle(bytes, BaBytes);
        Vector128<byte> red = Vector128.ShiftRightLogical(rg.AsUInt16(), 4).AsByte() & Vector128.Create((byte)15);
        Vector128<byte> green = rg & Vector128.Create((byte)15);
        Vector128<byte> blue = Vector128.ShiftRightLogical(ba.AsUInt16(), 4).AsByte() & Vector128.Create((byte)15);
        Vector128<byte> alpha = ba & Vector128.Create((byte)15);
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(red)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(green)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(blue)).AsUInt32());
        c3 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(alpha)).AsUInt32());
    }
}
