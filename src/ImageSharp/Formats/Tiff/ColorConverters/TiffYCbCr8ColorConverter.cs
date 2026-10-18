// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Converts TIFF YCbCr samples and packs byte RGB through the shared TIFF traversal.
/// </summary>
internal readonly struct TiffYCbCr8ColorConverter : ITiffColorPipeline<Rgba32>
{
    private const uint OpaqueAlpha = (uint)byte.MaxValue << 24;

    private readonly TiffYCbCr8Source source;
    private readonly TiffYCbCrOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffYCbCr8ColorConverter"/> struct.
    /// </summary>
    /// <param name="source">The packed or planar byte layout.</param>
    /// <param name="colorOperator">The TIFF reference ranges and luma matrix.</param>
    public TiffYCbCr8ColorConverter(TiffYCbCr8Source source, in TiffYCbCrOperator colorOperator)
    {
        this.source = source;
        this.colorOperator = colorOperator;
    }

    /// <inheritdoc />
    public static int Pixels512 => Vector512<float>.Count;

    /// <inheritdoc />
    public static int Pixels256 => Vector256<float>.Count;

    /// <inheritdoc />
    public static int Pixels128 => Vector128<float>.Count;

    /// <inheritdoc />
    public static int DestinationElementsPerPixel => 1;

    /// <summary>
    /// Converts packed or planar component bytes into one byte-quantized RGB row.
    /// </summary>
    /// <param name="source0">Packed bytes or the luminance plane.</param>
    /// <param name="source1">The planar blue-difference bytes, if present.</param>
    /// <param name="source2">The planar red-difference bytes, if present.</param>
    /// <param name="destination">The byte-quantized RGB pixels.</param>
    /// <param name="source">The source layout reader.</param>
    /// <param name="colorOperator">The TIFF reference-range and luma matrix.</param>
    public static void Convert(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        Span<Rgba32> destination,
        TiffYCbCr8Source source,
        in TiffYCbCrOperator colorOperator)
    {
        TiffYCbCr8ColorConverter pipeline = new(source, colorOperator);

        // One output register holds one complete group: sixteen RGBA32 pixels at 512 bits.
        // Shared traversal consumes every full group before selecting smaller registers and the scalar tail.
        TiffColorTraversal<TiffYCbCr8ColorConverter, Rgba32>.Convert(source0, source1, source2, [], destination, destination.Length, pipeline);
    }

    /// <inheritdoc />
    public void Convert512(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<Rgba32> destination,
        int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector512<float> y, out Vector512<float> cb, out Vector512<float> cr, out _);
        this.colorOperator.ConvertByte(y, cb, cr, out Vector512<int> r, out Vector512<int> g, out Vector512<int> b);

        // The operator has clipped each integer to a byte. Shift RGB into disjoint byte positions and add opaque alpha.
        // Each uint lane is one RGBA pixel, so one full-width store writes all sixteen pixels without narrowing or shuffling.
        Vector512<uint> packed = r.AsUInt32() | (g.AsUInt32() << 8) | (b.AsUInt32() << 16) | Vector512.Create(OpaqueAlpha);
        packed.StoreUnsafe(ref Unsafe.As<Rgba32, uint>(ref MemoryMarshal.GetReference(destination)), (nuint)index);
    }

    /// <inheritdoc />
    public void Convert256(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<Rgba32> destination,
        int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector256<float> y, out Vector256<float> cb, out Vector256<float> cr, out _);
        this.colorOperator.ConvertByte(y, cb, cr, out Vector256<int> r, out Vector256<int> g, out Vector256<int> b);

        // Eight independent uint lanes pack eight complete RGBA pixels, using the same byte positions as the wider path.
        Vector256<uint> packed = r.AsUInt32() | (g.AsUInt32() << 8) | (b.AsUInt32() << 16) | Vector256.Create(OpaqueAlpha);
        packed.StoreUnsafe(ref Unsafe.As<Rgba32, uint>(ref MemoryMarshal.GetReference(destination)), (nuint)index);
    }

    /// <inheritdoc />
    public void Convert128(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<Rgba32> destination,
        int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out Vector128<float> y, out Vector128<float> cb, out Vector128<float> cr, out _);
        this.colorOperator.ConvertByte(y, cb, cr, out Vector128<int> r, out Vector128<int> g, out Vector128<int> b);

        // Four byte-bounded channel lanes become four complete pixels; no lane can spill bits into the next channel.
        Vector128<uint> packed = r.AsUInt32() | (g.AsUInt32() << 8) | (b.AsUInt32() << 16) | Vector128.Create(OpaqueAlpha);
        packed.StoreUnsafe(ref Unsafe.As<Rgba32, uint>(ref MemoryMarshal.GetReference(destination)), (nuint)index);
    }

    /// <inheritdoc />
    public void ConvertScalar(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        Span<Rgba32> destination,
        int index)
    {
        this.source.Read(source0, source1, source2, source3, index, out float y, out float cb, out float cr, out _);
        this.colorOperator.ConvertByte(y, cb, cr, out byte r, out byte g, out byte b);
        destination[index] = new Rgba32(r, g, b);
    }
}
