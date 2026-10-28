// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsPixelCompatible
{
    /// <summary>
    /// Converts the pixel data of the specified image from the source color profile to the target color profile using
    /// the provided color profile converter.
    /// </summary>
    /// <remarks>
    /// This method modifies the source image in place by converting its pixel data according to the
    /// color profiles specified in the converter. The method does not verify whether the profiles are RGB compatible;
    /// if they are not, the conversion may produce incorrect results. Ensure that both the source and target ICC
    /// profiles are set on the converter before calling this method.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="source">
    /// The image whose pixel data will be converted. The conversion is performed in place, modifying the original
    /// image.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the converter's source or target ICC profile is not specified.
    /// </exception>
    public static void Convert<TPixel>(this ColorProfileConverter converter, Image<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // These checks actually take place within the converter, but we want to fail fast here.
        // Note. we do not check to see whether the profiles themselves are RGB compatible,
        // if they are not, then the converter will simply produce incorrect results.
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        // Process the rows in parallel chunks, the converter itself is thread safe.
        // Request straight scaled vectors on both sides of ICC. The processor uses the destination pixel's rules to restore association.
        source.Mutate(o => o.ProcessPixelRowsAsVector4(
            row =>
            {
                // Sixteen RGB pixels occupy 192 bytes and supply 16 lanes per channel for Vector512<float>.
                // Limit each call to this capacity and reuse the storage instead of renting another row buffer.
                // The converter writes RGB back into the processor's vector row and preserves its alpha.
                // This inline storage has no cache-line alignment guarantee; SIMD loads support unaligned addresses.
                InlineArray16<Rgb> storage = default;
                Span<Rgb> rgb = storage;

                while (!row.IsEmpty)
                {
                    int length = Math.Min(row.Length, rgb.Length);
                    converter.Convert(row[..length], rgb[..length]);
                    row = row[length..];
                }
            },
            PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
    }

    /// <summary>
    /// Converts source pixels from the converter's source profile to its target profile as straight, scaled vectors.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="source">The source pixels, which remain unchanged.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="scratch">Caller-owned RGB scratch storage.</param>
    public static void Convert<TPixel>(
        this ColorProfileConverter converter, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination, Span<Rgb> scratch)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // ICC consumes straight RGB. Extract associated pixels as straight vectors without changing the source pixels.
        PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, source, destination);

        converter.Convert(destination[..source.Length], scratch[..source.Length]);
    }

    /// <summary>
    /// Converts encoded colors in place through the configured ICC profiles and packs them in the destination pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="converter">The converter configured with source and target ICC profiles.</param>
    /// <param name="colors">The encoded colors to convert.</param>
    public static void Convert<TPixel>(this ColorProfileConverter converter, Span<Color> colors)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Vector4> vectorBuffer = converter.Options.MemoryAllocator.Allocate<Vector4>(colors.Length);
        using IMemoryOwner<Rgb> rgbBuffer = converter.Options.MemoryAllocator.Allocate<Rgb>(colors.Length);
        Span<Vector4> vectors = vectorBuffer.Memory.Span;

        // Color preserves the encoded sample precision and alpha until the ICC result reaches the destination pixel format.
        for (int i = 0; i < colors.Length; i++)
        {
            vectors[i] = colors[i].ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        }

        converter.Convert(vectors[..colors.Length], rgbBuffer.Memory.Span[..colors.Length]);

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.FromPixel(TPixel.FromUnassociatedScaledVector4(vectors[i]));
        }
    }

    /// <summary>
    /// Converts a color through the configured ICC profiles and returns it with the destination pixel format's precision and alpha representation.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="converter">The converter configured with source and target ICC profiles.</param>
    /// <param name="color">The encoded color to convert.</param>
    /// <returns>The converted color.</returns>
    public static Color Convert<TPixel>(this ColorProfileConverter converter, Color color)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4 source = color.ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        Rgb converted = converter.ConvertUsingIccProfile<Rgb, Rgb>(Rgb.FromScaledVector4(source));

        // ICC transforms straight RGB; the original alpha is packed by the destination pixel's own rules.
        Vector4 result = new(converted.ToScaledVector3(), source.W);
        return Color.FromPixel(TPixel.FromUnassociatedScaledVector4(result));
    }

    /// <summary>
    /// Converts scaled vector RGB components in place while preserving their alpha components.
    /// </summary>
    /// <remarks>
    /// RGB remains unclipped. Destination pixel conversion applies integer bounds; floating-point pixels retain extended values.
    /// </remarks>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="row">The scaled vectors whose RGB components are converted.</param>
    /// <param name="rgbSpan">RGB scratch storage with the same length as <paramref name="row"/>.</param>
    public static void Convert(this ColorProfileConverter converter, Span<Vector4> row, Span<Rgb> rgbSpan)
    {
        // ICC transforms RGB only. Keep alpha in the vector row while the RGB scratch span is converted in place.
        Rgb.FromScaledVector4(row, rgbSpan);
        converter.ConvertUsingIccProfile<Rgb, Rgb>(rgbSpan, rgbSpan);

        ref float srcRgb = ref Unsafe.As<Rgb, float>(ref MemoryMarshal.GetReference(rgbSpan));
        ref float dstRow = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row));
        int count = rgbSpan.Length;
        int i = 0;

        if (Avx512F.IsSupported)
        {
            // Four RGB pixels occupy 12 floats: r0 g0 b0, continuing through r3 g3 b3.
            // The 16-float load also reads four floats from the following pixels.
            // The permutation inserts a placeholder after each RGB triple to match the Vector4 destination.
            // The blend replaces RGB and retains the destination alpha in lanes 3, 7, 11 and 15.
            Vector512<int> rgbPerm = Vector512.Create(0, 1, 2, 0, 3, 4, 5, 0, 6, 7, 8, 0, 9, 10, 11, 0);

            // Negative zero sets the mask's sign bit, so BlendVariable selects the converted RGB in those lanes.
            Vector512<float> rgbSelect = Vector512.Create(-0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F);

            int quads = count >> 2;

            // Leave the final group for the scalar loop because its 16-float load could exceed the RGB span.
            int simdQuads = quads - 1;

            for (int q = 0; q < simdQuads; q++)
            {
                Vector512<float> dst = Vector512.LoadUnsafe(ref dstRow);
                Vector512<float> src = Vector512.LoadUnsafe(ref srcRgb);

                Vector512<float> rgbx = Avx512F.PermuteVar16x32(src, rgbPerm);
                Vector512<float> merged = Avx512F.BlendVariable(dst, rgbx, rgbSelect);

                merged.StoreUnsafe(ref dstRow);

                // Advance input by 4 pixels (4 * 3 = 12 floats)
                srcRgb = ref Unsafe.Add(ref srcRgb, 12U);

                // Advance output by 4 pixels (4 * 4 = 16 floats)
                dstRow = ref Unsafe.Add(ref dstRow, 16U);

                i += 4;
            }
        }
        else if (Avx2.IsSupported)
        {
            // 2 pixels per iteration.
            //
            // Same idea as AVX-512, but on 256-bit vectors.
            // We permute packed RGB into rgbx layout and blend into the existing destination,
            // preserving alpha lanes.
            Vector256<int> rgbPerm = Vector256.Create(0, 1, 2, 0, 3, 4, 5, 0);

            Vector256<float> rgbSelect = Vector256.Create(-0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F);

            int pairs = count >> 1;
            int simdPairs = pairs - 1; // Leave the last pair for the scalar tail to avoid the final overlapped load reading past the end.

            for (int p = 0; p < simdPairs; p++)
            {
                Vector256<float> dst = Vector256.LoadUnsafe(ref dstRow);
                Vector256<float> src = Vector256.LoadUnsafe(ref srcRgb);

                Vector256<float> rgbx = Avx2.PermuteVar8x32(src, rgbPerm);
                Vector256<float> merged = Avx.BlendVariable(dst, rgbx, rgbSelect);

                merged.StoreUnsafe(ref dstRow);

                // Advance input by 2 pixels (2 * 3 = 6 floats)
                srcRgb = ref Unsafe.Add(ref srcRgb, 6U);

                // Advance output by 2 pixels (2 * 4 = 8 floats)
                dstRow = ref Unsafe.Add(ref dstRow, 8U);

                i += 2;
            }
        }

        // Scalar tail.
        // Handles:
        // - the last skipped SIMD block (quad or pair)
        // - any remainder
        //
        // Preserve alpha by writing Vector3 into the Vector4 storage.
        ref Vector4 rowRef = ref MemoryMarshal.GetReference(row);
        for (; i < count; i++)
        {
            Vector3 rgb = rgbSpan[i].AsVector3Unsafe();
            Unsafe.As<Vector4, Vector3>(ref Unsafe.Add(ref rowRef, (uint)i)) = rgb;
        }
    }
}
