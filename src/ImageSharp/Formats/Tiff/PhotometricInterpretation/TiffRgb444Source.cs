// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads three interleaved four-bit RGB components in TIFF most-significant-nibble order.
/// </summary>
internal readonly struct TiffRgb444Source : ITiffNibbleColorSource
{
    private static readonly Vector128<byte> RedBytes = Vector128.Create((byte)0, 1, 3, 6, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
    private static readonly Vector128<byte> GreenBytes = Vector128.Create((byte)0, 2, 3, 7, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
    private static readonly Vector128<byte> BlueBytes = Vector128.Create((byte)1, 2, 6, 7, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
    private static readonly Vector128<byte> EvenLanes = Vector128.Create((byte)255, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <inheritdoc />
    public int BitsPerPixel => 12;

    /// <inheritdoc />
    public int ComponentCount => 3;

    /// <inheritdoc />
    public void ReadFour(ReadOnlySpan<byte> row, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        ReadOnlySpan<byte> packed = row.Slice((index / 2) * 3);

        // Four RGB pixels occupy six bytes. Two overlapping four-byte reads keep the final group in bounds.
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<uint>(packed), MemoryMarshal.Read<uint>(packed.Slice(2)), 0U, 0U).AsByte();
        Vector128<byte> red = Vector128.Shuffle(bytes, RedBytes);
        Vector128<byte> green = Vector128.Shuffle(bytes, GreenBytes);
        Vector128<byte> blue = Vector128.Shuffle(bytes, BlueBytes);
        Vector128<byte> redCodes = Vector128.ConditionalSelect(EvenLanes, Vector128.ShiftRightLogical(red.AsUInt16(), 4).AsByte(), red) & Vector128.Create((byte)15);
        Vector128<byte> greenCodes = Vector128.ConditionalSelect(EvenLanes, green, Vector128.ShiftRightLogical(green.AsUInt16(), 4).AsByte()) & Vector128.Create((byte)15);
        Vector128<byte> blueCodes = Vector128.ConditionalSelect(EvenLanes, Vector128.ShiftRightLogical(blue.AsUInt16(), 4).AsByte(), blue) & Vector128.Create((byte)15);
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(redCodes)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(greenCodes)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(blueCodes)).AsUInt32());
        c3 = Vector128<float>.Zero;
    }
}
