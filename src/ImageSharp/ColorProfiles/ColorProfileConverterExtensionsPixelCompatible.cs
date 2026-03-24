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
        source.Mutate(o => o.ProcessPixelRowsAsVector4(
            row =>
            {
                // Gather and convert the pixels in the row to Rgb.
                using IMemoryOwner<Rgb> rgbBuffer = converter.Options.MemoryAllocator.Allocate<Rgb>(row.Length);
                Span<Rgb> rgbSpan = rgbBuffer.Memory.Span;
                Rgb.FromScaledVector4(row, rgbSpan);

                // Perform the actual color conversion.
                converter.ConvertUsingIccProfile<Rgb, Rgb>(rgbSpan, rgbSpan);

                // Copy the converted Rgb pixels back to the row as TPixel.
                // Important: Preserve alpha from the existing row Vector4 values.
                // We merge RGB from rgbSpan into row, leaving W untouched.
                ref float srcRgb = ref Unsafe.As<Rgb, float>(ref MemoryMarshal.GetReference(rgbSpan));
                ref float dstRow = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(row));

                int count = rgbSpan.Length;
                int i = 0;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static Vector512<float> ReadVector512(ref float f)
                {
                    ref byte b = ref Unsafe.As<float, byte>(ref f);
                    return Unsafe.ReadUnaligned<Vector512<float>>(ref b);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static void WriteVector512(ref float f, Vector512<float> v)
                {
                    ref byte b = ref Unsafe.As<float, byte>(ref f);
                    Unsafe.WriteUnaligned(ref b, v);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static Vector256<float> ReadVector256(ref float f)
                {
                    ref byte b = ref Unsafe.As<float, byte>(ref f);
                    return Unsafe.ReadUnaligned<Vector256<float>>(ref b);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static void WriteVector256(ref float f, Vector256<float> v)
                {
                    ref byte b = ref Unsafe.As<float, byte>(ref f);
                    Unsafe.WriteUnaligned(ref b, v);
                }

                if (Avx512F.IsSupported)
                {
                    // 4 pixels per iteration.
                    //
                    // Source layout (Rgb float stream, 12 floats):
                    // [r0 g0 b0 r1 g1 b1 r2 g2 b2 r3 g3 b3]
                    //
                    // Destination layout (row Vector4 float stream, 16 floats):
                    // [r0 g0 b0 a0 r1 g1 b1 a1 r2 g2 b2 a2 r3 g3 b3 a3]
                    //
                    // We use an overlapped load (16 floats) from the 3-float stride source.
                    // The permute selects the RGB we need and inserts placeholders for alpha lanes.
                    //
                    // Then we blend RGB lanes into the existing destination, preserving alpha lanes.
                    Vector512<int> rgbPerm = Vector512.Create(0, 1, 2, 0, 3, 4, 5, 0, 6, 7, 8, 0, 9, 10, 11, 0);

                    // BlendVariable selects from the second operand where the sign bit of the mask lane is set.
                    // We want to overwrite lanes 0,1,2 then 4,5,6 then 8,9,10 then 12,13,14, and preserve lanes 3,7,11,15 (alpha).
                    Vector512<float> rgbSelect = Vector512.Create(-0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F, -0F, -0F, -0F, 0F);

                    int quads = count >> 2;
                    int simdQuads = quads - 1; // Leave the last quad for the scalar tail to avoid the final overlapped load reading past the end.

                    for (int q = 0; q < simdQuads; q++)
                    {
                        Vector512<float> dst = ReadVector512(ref dstRow);
                        Vector512<float> src = ReadVector512(ref srcRgb);

                        Vector512<float> rgbx = Avx512F.PermuteVar16x32(src, rgbPerm);
                        Vector512<float> merged = Avx512F.BlendVariable(dst, rgbx, rgbSelect);

                        WriteVector512(ref dstRow, merged);

                        // Advance input by 4 pixels (4 * 3 = 12 floats)
                        srcRgb = ref Unsafe.Add(ref srcRgb, 12);

                        // Advance output by 4 pixels (4 * 4 = 16 floats)
                        dstRow = ref Unsafe.Add(ref dstRow, 16);

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
                        Vector256<float> dst = ReadVector256(ref dstRow);
                        Vector256<float> src = ReadVector256(ref srcRgb);

                        Vector256<float> rgbx = Avx2.PermuteVar8x32(src, rgbPerm);
                        Vector256<float> merged = Avx.BlendVariable(dst, rgbx, rgbSelect);

                        WriteVector256(ref dstRow, merged);

                        // Advance input by 2 pixels (2 * 3 = 6 floats)
                        srcRgb = ref Unsafe.Add(ref srcRgb, 6);

                        // Advance output by 2 pixels (2 * 4 = 8 floats)
                        dstRow = ref Unsafe.Add(ref dstRow, 8);

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
            },
            PixelConversionModifiers.Scale));
    }
}
