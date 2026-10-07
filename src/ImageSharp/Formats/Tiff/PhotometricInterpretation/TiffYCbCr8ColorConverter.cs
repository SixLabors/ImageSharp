// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Traverses TIFF YCbCr rows through source loading, matrix conversion, and byte packing.
/// </summary>
internal static class TiffYCbCr8ColorConverter
{
    private static readonly Vector128<byte> RgbaIndices = Vector128.Create((byte)0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15);
    private static readonly Vector128<uint> OpaqueAlpha = Vector128.Create(255U);

    /// <summary>
    /// Converts packed or planar component bytes into one byte-quantized RGB row.
    /// </summary>
    /// <param name="source0">Packed bytes or the luminance plane.</param>
    /// <param name="source1">The planar blue-difference bytes, if present.</param>
    /// <param name="source2">The planar red-difference bytes, if present.</param>
    /// <param name="destination">The byte-quantized RGB pixels.</param>
    /// <param name="source">The source layout reader.</param>
    /// <param name="colorOperator">The TIFF reference-range and luma matrix.</param>
    public static void Convert(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, Span<Rgba32> destination, TiffYCbCr8Source source, in TiffYCbCrOperator colorOperator)
    {
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int lastVector = destination.Length - Vector512<float>.Count;

            for (; i <= lastVector; i += Vector512<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector512<float> y, out Vector512<float> cb, out Vector512<float> cr, out _);
                colorOperator.ConvertByte(y, cb, cr, out Vector512<int> r, out Vector512<int> g, out Vector512<int> b);
                PackFour(r.GetLower().GetLower(), g.GetLower().GetLower(), b.GetLower().GetLower(), destination.Slice(i));
                PackFour(r.GetLower().GetUpper(), g.GetLower().GetUpper(), b.GetLower().GetUpper(), destination.Slice(i + 4));
                PackFour(r.GetUpper().GetLower(), g.GetUpper().GetLower(), b.GetUpper().GetLower(), destination.Slice(i + 8));
                PackFour(r.GetUpper().GetUpper(), g.GetUpper().GetUpper(), b.GetUpper().GetUpper(), destination.Slice(i + 12));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int lastVector = destination.Length - Vector256<float>.Count;

            for (; i <= lastVector; i += Vector256<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector256<float> y, out Vector256<float> cb, out Vector256<float> cr, out _);
                colorOperator.ConvertByte(y, cb, cr, out Vector256<int> r, out Vector256<int> g, out Vector256<int> b);
                PackFour(r.GetLower(), g.GetLower(), b.GetLower(), destination.Slice(i));
                PackFour(r.GetUpper(), g.GetUpper(), b.GetUpper(), destination.Slice(i + 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int lastVector = destination.Length - Vector128<float>.Count;

            for (; i <= lastVector; i += Vector128<float>.Count)
            {
                source.Read(source0, source1, source2, [], i, out Vector128<float> y, out Vector128<float> cb, out Vector128<float> cr, out _);
                colorOperator.ConvertByte(y, cb, cr, out Vector128<int> r, out Vector128<int> g, out Vector128<int> b);
                PackFour(r, g, b, destination.Slice(i));
            }
        }

        for (; i < destination.Length; i++)
        {
            source.Read(source0, source1, source2, [], i, out float y, out float cb, out float cr, out _);
            colorOperator.ConvertByte(y, cb, cr, out byte r, out byte g, out byte b);
            destination[i] = new Rgba32(r, g, b);
        }
    }

    /// <summary>
    /// Narrows four byte-range channel registers into four opaque RGBA pixels.
    /// </summary>
    /// <param name="r">Four rounded red codes.</param>
    /// <param name="g">Four rounded green codes.</param>
    /// <param name="b">Four rounded blue codes.</param>
    /// <param name="destination">Four destination pixels.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PackFour(Vector128<int> r, Vector128<int> g, Vector128<int> b, Span<Rgba32> destination)
    {
        // Narrow separate channels then shuffle [rrrr gggg bbbb aaaa] into RGBA pixels.
        Vector128<ushort> rg = Vector128.Narrow(r.AsUInt32(), g.AsUInt32());
        Vector128<ushort> ba = Vector128.Narrow(b.AsUInt32(), OpaqueAlpha);
        Vector128<byte> channels = Vector128.Narrow(rg, ba);
        Vector128<byte> pixels = Vector128.ShuffleNative(channels, RgbaIndices);
        pixels.StoreUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Rgba32, byte>(destination)));
    }
}
