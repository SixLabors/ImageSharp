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
    /// The profile-free converter for the format-defined YCbCr-to-RGB and YccK-to-CMYK models. Its options never
    /// change, so every image and thread shares it.
    /// </summary>
    private static readonly ColorProfileConverter ModelConverter = new();

    /// <summary>
    /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space in-place using the given ICC profile.
    /// </summary>
    /// <param name="configuration">The configuration instance to use for the conversion.</param>
    /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct.</param>
    /// <param name="profileConverter">The converter from the source ICC profile to sRGB, reused for every row.</param>
    /// <param name="grayscaleRgbBytes">Optional interleaved RGB byte output for grayscale ICC; when empty, grayscale output uses the component planes.</param>
    public void ConvertToRgbInPlaceWithIcc(
        Configuration configuration,
        in ComponentValues values,
        ColorProfileConverter profileConverter,
        Span<byte> grayscaleRgbBytes = default)
    {
        JpegColorSpace componentColorSpace = this.ColorSpace;
        Span<float> c0 = values.Component0;
        Span<float> c1 = values.Component1;
        Span<float> c2 = values.Component2;
        int length = c0.Length;

        // Four-component JPEG models need room for an interleaved CMYK or YccK source. Every conversion
        // finishes with three packed RGB floats, which safely occupy the start of the same temporary buffer.
        bool hasFourthComponent = componentColorSpace is JpegColorSpace.Ycck
            or JpegColorSpace.Cmyk
            or JpegColorSpace.TiffYccK
            or JpegColorSpace.TiffCmyk;

        int packedComponentCount = hasFourthComponent ? 4 : 3;

        using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(length * packedComponentCount);
        Span<float> packed = memoryOwner.Memory.Span;

        if (componentColorSpace == JpegColorSpace.Grayscale)
        {
            // The single luminance plane is the ICC source, so it is normalized in place. The temporary
            // buffer is still RGB-sized because the profile conversion expands each Y sample to three lanes.
            TensorPrimitives.Multiply(c0, 1F / this.MaximumValue, c0);

            Span<Y> source = MemoryMarshal.Cast<float, Y>(c0);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed);
            profileConverter.Convert<Y, Rgb>(source, destination);

            if (!grayscaleRgbBytes.IsEmpty)
            {
                // The packed ICC result can fill the existing RGB byte row directly; scattering into aliased grayscale planes would overwrite channels.
                SimdUtils.NormalizedFloatToByteSaturate(packed[..grayscaleRgbBytes.Length], grayscaleRgbBytes);
                return;
            }

            SimdUtils.UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..length], c0, c1, c2);
            return;
        }

        // RGB planes and YCbCr with an RGB profile become packed RGB before ICC. A YCbCr source profile
        // consumes packed YCbCr directly; CMYK models retain their four-component source space.
        bool profileSourceIsCmyk = false;
        bool profileSourceIsYCbCr = componentColorSpace == JpegColorSpace.YCbCr
            && profileConverter.Options.SourceIccProfile.Header.DataColorSpace == IccColorSpaceType.YCbCr;

        switch (componentColorSpace)
        {
            case JpegColorSpace.RGB:
                // JPEG RGB planes use the integer sample domain; ICC RGB values use normalized interleaved lanes.
                SimdUtils.PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / this.MaximumValue);
                break;

            case JpegColorSpace.YCbCr:
                // A YCbCr ICC profile consumes normalized components directly; an RGB ICC profile consumes
                // RGB converted from the stored YCbCr components before the profile transform.
                SimdUtils.PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / this.MaximumValue);

                if (!profileSourceIsYCbCr)
                {
                    // ICC profiles rarely expose YCbCr transforms. BT.601 therefore produces RGB in the profile's
                    // source space first, and that packed RGB becomes the input to the profile transform below.
                    Span<YCbCr> yCbCr = MemoryMarshal.Cast<float, YCbCr>(packed);
                    Span<Rgb> yCbCrDestination = MemoryMarshal.Cast<float, Rgb>(packed);
                    ModelConverter.Convert<YCbCr, Rgb>(yCbCr, yCbCrDestination);
                }

                break;

            case JpegColorSpace.Cmyk:
                // Adobe-style JPEG CMYK stores inverted samples, while ICC consumes conventional normalized CMYK.
                SimdUtils.PackedInvertNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.TiffCmyk:
                // TIFF JPEG CMYK is already non-inverted, so only normalization and interleaving are required.
                SimdUtils.PackedNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.Ycck:
                // Adobe-style JPEG YccK is inverted before its format-defined YccK-to-CMYK transform.
                SimdUtils.PackedInvertNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);

                Span<Cmyk> yccKCmyk = MemoryMarshal.Cast<float, Cmyk>(packed);
                ModelConverter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(yccKCmyk), yccKCmyk);
                profileSourceIsCmyk = true;
                break;

            case JpegColorSpace.TiffYccK:
                // TIFF JPEG YccK is non-inverted, but otherwise uses the same YccK-to-CMYK transform.
                SimdUtils.PackedNormalizeInterleave4(c0, c1, c2, values.Component3, packed, this.MaximumValue);

                Span<Cmyk> tiffYccKCmyk = MemoryMarshal.Cast<float, Cmyk>(packed);
                ModelConverter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(tiffYccKCmyk), tiffYccKCmyk);
                profileSourceIsCmyk = true;
                break;
        }

        Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(packed)[..length];

        if (profileSourceIsCmyk)
        {
            // The destination aliases the first three floats of each four-float source item. The converter
            // supports this established in-place contraction, and the source span retains its original length.
            profileConverter.Convert<Cmyk, Rgb>(MemoryMarshal.Cast<float, Cmyk>(packed), rgb);
        }
        else if (profileSourceIsYCbCr)
        {
            // A YCbCr source profile consumes normalized component values directly, before any RGB matrix.
            profileConverter.Convert<YCbCr, Rgb>(MemoryMarshal.Cast<float, YCbCr>(packed)[..length], rgb);
        }
        else
        {
            profileConverter.Convert<Rgb, Rgb>(rgb, rgb);
        }

        // Only the packed RGB prefix is meaningful after four-component conversion; scatter it back to the
        // decoder's three planar output buffers while leaving the fourth source component untouched.
        SimdUtils.UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..length], c0, c1, c2);
    }
}
