// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides utilities for encoding images.
/// </summary>
internal static class EncodingUtilities
{
    /// <summary>
    /// Determines if transparent pixels can be replaced based on the specified color mode and pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="mode">Indicates the color mode used to assess the ability to replace transparent pixels.</param>
    /// <returns>Returns true if transparent pixels can be replaced; otherwise, false.</returns>
    public static bool ShouldReplaceTransparentPixels<TPixel>(TransparentColorMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
        => mode == TransparentColorMode.Clear && TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> where the transparent pixels will be changed.</param>
    public static void ReplaceTransparentPixels<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
        => ReplaceTransparentPixels(frame.Configuration, frame.PixelBuffer);

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="buffer">The  <see cref="Buffer2D{TPixel}"/> where the transparent pixels will be changed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReplaceTransparentPixels<TPixel>(Configuration configuration, Buffer2D<TPixel> buffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2DRegion<TPixel> region = buffer.GetRegion();
        ReplaceTransparentPixels(configuration, in region);
    }

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="region">The <see cref="Buffer2DRegion{T}"/> where the transparent pixels will be changed.</param>
    public static void ReplaceTransparentPixels<TPixel>(
        Configuration configuration,
        in Buffer2DRegion<TPixel> region)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(region.Width);
        Span<Vector4> vectorsSpan = vectors.GetSpan();
        for (int y = 0; y < region.Height; y++)
        {
            Span<TPixel> span = region.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToVector4(configuration, span, vectorsSpan, PixelConversionModifiers.Scale);
            ReplaceTransparentPixels(vectorsSpan);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorsSpan, span, PixelConversionModifiers.Scale);
        }
    }

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <param name="source">A span of color vectors that will be checked for transparency and potentially modified.</param>
    public static void ReplaceTransparentPixels(Span<Vector4> source)
    {
        if (Vector512.IsHardwareAccelerated && source.Length >= 4)
        {
            Span<Vector512<float>> source512 = MemoryMarshal.Cast<Vector4, Vector512<float>>(source);
            for (int i = 0; i < source512.Length; i++)
            {
                ref Vector512<float> v = ref source512[i];

                // Do `vector < threshold`
                Vector512<float> mask = Vector512.Equals(v, Vector512<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector512.Shuffle(mask, Vector512.Create(3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v512 & ~mask)
                v = Vector512.ConditionalSelect(mask, Vector512<float>.Zero, v);
            }

            int m = Numerics.Modulo4(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W == 0)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && source.Length >= 2)
        {
            Span<Vector256<float>> source256 = MemoryMarshal.Cast<Vector4, Vector256<float>>(source);
            for (int i = 0; i < source256.Length; i++)
            {
                ref Vector256<float> v = ref source256[i];

                // Do `vector < threshold`
                Vector256<float> mask = Vector256.Equals(v, Vector256<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector256.Shuffle(mask, Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v256 & ~mask)
                v = Vector256.ConditionalSelect(mask, Vector256<float>.Zero, v);
            }

            int m = Numerics.Modulo2(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W == 0)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (int i = 0; i < source.Length; i++)
            {
                ref Vector4 v = ref source[i];
                Vector128<float> v128 = v.AsVector128();

                // Do `vector == 0`
                Vector128<float> mask = Vector128.Equals(v128, Vector128<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector128.Shuffle(mask, Vector128.Create(3, 3, 3, 3));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v128 & ~mask)
                v = Vector128.ConditionalSelect(mask, Vector128<float>.Zero, v128).AsVector4();
            }
        }
        else
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].W == 0F)
                {
                    source[i] = Vector4.Zero;
                }
            }
        }
    }
}
