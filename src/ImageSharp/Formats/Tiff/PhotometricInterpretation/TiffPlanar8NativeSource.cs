// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four eight-bit TIFF component planes without color conversion.
/// </summary>
internal readonly struct TiffPlanar8NativeSource : ITiffColorSource<byte>
{
    /// <inheritdoc/>
    public int BytesPerPixel => 1;

    /// <inheritdoc/>
    public int ComponentCount => 4;

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out byte c0,
        out byte c1,
        out byte c2,
        out byte c3)
    {
        c0 = source0[index];
        c1 = source1[index];
        c2 = source2[index];
        c3 = source3[index];
    }

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector128<byte> c0,
        out Vector128<byte> c1,
        out Vector128<byte> c2,
        out Vector128<byte> c3)
    {
        c0 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source0), (nuint)index);
        c1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source1), (nuint)index);
        c2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source2), (nuint)index);
        c3 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source3), (nuint)index);
    }

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<byte> c0,
        out Vector256<byte> c1,
        out Vector256<byte> c2,
        out Vector256<byte> c3)
    {
        c0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(source0), (nuint)index);
        c1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(source1), (nuint)index);
        c2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(source2), (nuint)index);
        c3 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(source3), (nuint)index);
    }

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<byte> c0,
        out Vector512<byte> c1,
        out Vector512<byte> c2,
        out Vector512<byte> c3)
    {
        c0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(source0), (nuint)index);
        c1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(source1), (nuint)index);
        c2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(source2), (nuint)index);
        c3 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(source3), (nuint)index);
    }
}
