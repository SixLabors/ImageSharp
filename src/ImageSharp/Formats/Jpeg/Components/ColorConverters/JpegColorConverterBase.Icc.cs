// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space in-place using the given ICC profile.
    /// </summary>
    /// <param name="configuration">The configuration instance to use for the conversion.</param>
    /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct.</param>
    /// <param name="profile">The ICC profile to use for the conversion.</param>
    public void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
    {
        Span<float> c0 = values.Component0;
        Span<float> c1 = values.Component1;
        Span<float> c2 = values.Component2;
        int length = c0.Length;

        // Four-component JPEG models need room for an interleaved CMYK or YccK source. Every conversion
        // finishes with three packed RGB floats, which safely occupy the start of the same temporary buffer.
        bool hasFourthComponent = this.ColorSpace is JpegColorSpace.Ycck
            or JpegColorSpace.Cmyk
            or JpegColorSpace.TiffYccK
            or JpegColorSpace.TiffCmyk;
        int packedComponentCount = hasFourthComponent ? 4 : 3;

        using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(length * packedComponentCount);
        Span<float> packed = memoryOwner.Memory.Span;

        if (this.ColorSpace == JpegColorSpace.Grayscale)
        {
            // The single luminance plane is the ICC source, so it is normalized in place. The temporary
            // buffer is still RGB-sized because the profile conversion expands each Y sample to three lanes.
            TensorPrimitives.Multiply(c0, 1F / this.MaximumValue, c0);

            Span<Y> source = MemoryMarshal.Cast<float, Y>(c0);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed);
            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };

            ColorProfileConverter converter = new(options);
            converter.Convert<Y, Rgb>(source, destination);
            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..length], c0, c1, c2);
            return;
        }

        // RGB and YCbCr become packed RGB before the profile transform. CMYK models remain packed CMYK,
        // because their source profile describes that four-component space rather than an intermediate RGB space.
        bool profileSourceIsCmyk = false;

        switch (this.ColorSpace)
        {
            case JpegColorSpace.RGB:
                // JPEG RGB planes use the integer sample domain; ICC RGB values use normalized interleaved lanes.
                PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / this.MaximumValue);
                break;

            case JpegColorSpace.YCbCr:
                // ICC profiles rarely expose YCbCr transforms. BT.601 therefore produces RGB in the profile's
                // source space first, and that packed RGB becomes the input to the profile transform below.
                PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / this.MaximumValue);

                ColorProfileConverter yCbCrConverter = new();
                Span<YCbCr> yCbCr = MemoryMarshal.Cast<float, YCbCr>(packed);
                Span<Rgb> yCbCrDestination = MemoryMarshal.Cast<float, Rgb>(packed);
                yCbCrConverter.Convert<YCbCr, Rgb>(yCbCr, yCbCrDestination);
                break;

            case JpegColorSpace.Cmyk:
                // Adobe-style JPEG CMYK stores inverted samples, while ICC consumes conventional normalized CMYK.
                PackedInvertNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.TiffCmyk:
                // TIFF JPEG CMYK is already non-inverted, so only normalization and interleaving are required.
                PackedNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.Ycck:
                // Adobe-style JPEG YccK is inverted before its format-defined YccK-to-CMYK transform.
                PackedInvertNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);

                ColorProfileConverter yccKConverter = new();
                Span<Cmyk> yccKCmyk = MemoryMarshal.Cast<float, Cmyk>(packed);
                yccKConverter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(yccKCmyk), yccKCmyk);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.TiffYccK:
                // TIFF JPEG YccK is non-inverted, but otherwise uses the same YccK-to-CMYK transform.
                PackedNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);

                ColorProfileConverter tiffYccKConverter = new();
                Span<Cmyk> tiffYccKCmyk = MemoryMarshal.Cast<float, Cmyk>(packed);
                tiffYccKConverter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(tiffYccKCmyk), tiffYccKCmyk);
                profileSourceIsCmyk = true;
                break;
        }

        ColorConversionOptions profileOptions = new()
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
        };

        ColorProfileConverter profileConverter = new(profileOptions);
        Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed)[..length];

        if (profileSourceIsCmyk)
        {
            // The destination aliases the first three floats of each four-float source item. The converter
            // supports this established in-place contraction, and the source span retains its original length.
            profileConverter.Convert<Cmyk, Rgb>(MemoryMarshal.Cast<float, Cmyk>(packed), rgb);
        }
        else
        {
            profileConverter.Convert<Rgb, Rgb>(rgb, rgb);
        }

        // Only the packed RGB prefix is meaningful after four-component conversion; scatter it back to the
        // decoder's three planar output buffers while leaving the fourth source component untouched.
        UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..length], c0, c1, c2);
    }
}
