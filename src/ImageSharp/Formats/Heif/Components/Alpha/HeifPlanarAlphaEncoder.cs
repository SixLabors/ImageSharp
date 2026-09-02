// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Converts packed ImageSharp alpha values into one native HEIF monochrome plane.
/// </summary>
internal static class HeifPlanarAlphaEncoder
{
    /// <summary>
    /// Converts one packed image frame into a full-range native alpha plane.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the destination plane.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for row allocation and pixel conversion.</param>
    /// <param name="image">The packed source image frame.</param>
    /// <param name="buffer">The monochrome destination buffer.</param>
    public static void Convert<TPixel, TBuffer, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        TBuffer buffer)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        int width = image.Width;

        // Rgba64 preserves the source pixel's normalized alpha precision before quantization to the requested AV1
        // depth. Both row views share one owner because their lifetimes never escape this conversion operation.
        using IMemoryOwner<float> rowOwner = configuration.MemoryAllocator.Allocate<float>(width * 3);
        Span<float> rowStorage = rowOwner.GetSpan();
        Span<Rgba64> packed = MemoryMarshal.Cast<float, Rgba64>(rowStorage[..(width * 2)]);
        Span<float> alpha = rowStorage.Slice(width * 2, width);
        float maximum = (1 << buffer.LumaBitDepth) - 1;
        float scale = maximum / ushort.MaxValue;
        for (int y = 0; y < image.Height; y++)
        {
            ReadOnlySpan<TPixel> source = image.PixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgba64(configuration, source, packed);
            ExtractAlpha(packed, alpha);
            HeifSampleConversion.WriteSamples<TSample, TStorer>(
                alpha,
                buffer.GetLumaRowSpan(y),
                scale,
                0F,
                maximum);
        }
    }

    /// <summary>
    /// Deinterleaves alpha values from one packed high-precision row.
    /// </summary>
    private static void ExtractAlpha(ReadOnlySpan<Rgba64> source, Span<float> destination)
    {
        ref Rgba64 sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Packed RGBA requires a gather before conversion. Constructing vectors from the alpha fields keeps the
        // widening and stores SIMD-wide without copying or transposing the complete packed row.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector512Count<float>(destination.Length);
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                int offset = (int)(vectorIndex * (uint)Vector512<float>.Count);
                Vector512<uint> values = Vector512.Create(
                    CreateAlphaVector256(ref Unsafe.Add(ref sourceBase, offset)),
                    CreateAlphaVector256(ref Unsafe.Add(ref sourceBase, offset + Vector256<uint>.Count)));

                Vector512.ConvertToSingle(values).StoreUnsafe(
                    ref destinationBase,
                    (nuint)offset);
            }

            i = (int)(vectorCount * (uint)Vector512<float>.Count);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector256Count<float>(destination.Length - i);
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                int offset = i + (int)(vectorIndex * (uint)Vector256<float>.Count);
                Vector256<uint> values = CreateAlphaVector256(ref Unsafe.Add(ref sourceBase, offset));
                Vector256.ConvertToSingle(values).StoreUnsafe(
                    ref destinationBase,
                    (nuint)offset);
            }

            i += (int)(vectorCount * (uint)Vector256<float>.Count);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = Numerics.Vector128Count<float>(destination.Length - i);
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                int offset = i + (int)(vectorIndex * (uint)Vector128<float>.Count);
                Vector128<uint> values = CreateAlphaVector128(ref Unsafe.Add(ref sourceBase, offset));
                Vector128.ConvertToSingle(values).StoreUnsafe(
                    ref destinationBase,
                    (nuint)offset);
            }

            i += (int)(vectorCount * (uint)Vector128<float>.Count);
        }

        for (; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = Unsafe.Add(ref sourceBase, i).A;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CreateAlphaVector128(ref Rgba64 source)
        => Vector128.Create(
            (uint)source.A,
            Unsafe.Add(ref source, 1).A,
            Unsafe.Add(ref source, 2).A,
            Unsafe.Add(ref source, 3).A);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> CreateAlphaVector256(ref Rgba64 source)
        => Vector256.Create(
            CreateAlphaVector128(ref source),
            CreateAlphaVector128(ref Unsafe.Add(ref source, Vector128<uint>.Count)));
}
