// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        // Process the rows in parallel chnks, the converter itself is thread safe.
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
                ref Vector4 rowRef = ref MemoryMarshal.GetReference(row);
                for (int i = 0; i < rgbSpan.Length; i++)
                {
                    Vector3 rgb = rgbSpan[i].AsVector3Unsafe();
                    Unsafe.As<Vector4, Vector3>(ref Unsafe.Add(ref rowRef, i)) = rgb;
                }
            },
            PixelConversionModifiers.Scale));
    }
}
