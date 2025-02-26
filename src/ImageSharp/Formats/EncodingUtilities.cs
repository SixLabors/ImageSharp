// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides utilities for encoding images.
/// </summary>
internal static class EncodingUtilities
{
    public static bool ShouldClearTransparentPixels<TPixel>(TransparentColorMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
        => mode == TransparentColorMode.Clear &&
                   TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;

    /// <summary>
    /// Convert transparent pixels, to pixels represented by <paramref name="color"/>, which can yield
    /// to better compression in some cases.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> where the transparent pixels will be changed.</param>
    /// <param name="color">The color to replace transparent pixels with.</param>
    public static void ClearTransparentPixels<TPixel>(ImageFrame<TPixel> frame, Color color)
        where TPixel : unmanaged, IPixel<TPixel>
        => ClearTransparentPixels(frame.Configuration, frame.PixelBuffer, color);

    /// <summary>
    /// Convert transparent pixels, to pixels represented by <paramref name="color"/>, which can yield
    /// to better compression in some cases.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="buffer">The  <see cref="Buffer2D{TPixel}"/> where the transparent pixels will be changed.</param>
    /// <param name="color">The color to replace transparent pixels with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearTransparentPixels<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> buffer,
        Color color)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2DRegion<TPixel> region = buffer.GetRegion();
        ClearTransparentPixels(configuration, in region, color);
    }

    /// <summary>
    /// Convert transparent pixels, to pixels represented by <paramref name="color"/>, which can yield
    /// to better compression in some cases.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="region">The <see cref="Buffer2DRegion{T}"/> where the transparent pixels will be changed.</param>
    /// <param name="color">The color to replace transparent pixels with.</param>
    public static void ClearTransparentPixels<TPixel>(
        Configuration configuration,
        in Buffer2DRegion<TPixel> region,
        Color color)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(region.Width);
        Span<Vector4> vectorsSpan = vectors.GetSpan();
        Vector4 replacement = color.ToScaledVector4();
        for (int y = 0; y < region.Height; y++)
        {
            Span<TPixel> span = region.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToVector4(configuration, span, vectorsSpan, PixelConversionModifiers.Scale);
            ClearTransparentPixelRow(vectorsSpan, replacement);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorsSpan, span, PixelConversionModifiers.Scale);
        }
    }

    private static void ClearTransparentPixelRow(Span<Vector4> vectorsSpan, Vector4 replacement)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> replacement128 = replacement.AsVector128();

            for (int i = 0; i < vectorsSpan.Length; i++)
            {
                ref Vector4 v = ref vectorsSpan[i];
                Vector128<float> v128 = v.AsVector128();

                // Do `vector == 0`
                Vector128<float> mask = Vector128.Equals(v128, Vector128<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector128.Shuffle(mask, Vector128.Create(3, 3, 3, 3));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v128 & ~mask)
                v = Vector128.ConditionalSelect(mask, replacement128, v128).AsVector4();
            }
        }
        else
        {
            for (int i = 0; i < vectorsSpan.Length; i++)
            {
                if (vectorsSpan[i].W == 0F)
                {
                    vectorsSpan[i] = replacement;
                }
            }
        }
    }
}
