// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using IccColorProfile = SixLabors.ImageSharp.Metadata.Profiles.Icc.IccProfile;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

public partial class BmpEncoderTests
{
    /// <summary>
    /// Checks direct-color samples against whole-image conversion before BMP packing, without changing the source.
    /// </summary>
    /// <param name="provider">The existing high-depth or alpha fixture.</param>
    /// <param name="bitsPerPixel">The packed BMP layout.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, BmpBitsPerPixel.Bit16)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, BmpBitsPerPixel.Bit24)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, BmpBitsPerPixel.Bit32)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVector, BmpBitsPerPixel.Bit32)]
    public void Encode_IccProfile_TransformsSamplesWithoutChangingSource(TestImageProvider<RgbaVector> provider, BmpBitsPerPixel bitsPerPixel)
    {
        using Image<RgbaVector> source = provider.GetImage();
        using Image<RgbaVector> expected = provider.GetImage();
        using Image<RgbaVector> unchanged = provider.GetImage();

        IccColorProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccColorProfile imageProfile = source.Metadata.IccProfile;
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        // Convert the floating reference before encoding, so the requested BMP layout quantizes it only once.
        IccColorProfile.GetIccProfileForEncoding(profile, PixelColorType.RGB, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);

        converter.Convert(expected);
        expected.Metadata.IccProfile = null;
        expected.Frames.RootFrame.Metadata.IccProfile = null;

        foreach (bool skipMetadata in new[] { false, true })
        {
            BmpEncoder encoder = new() { BitsPerPixel = bitsPerPixel, SupportTransparency = true, SkipMetadata = skipMetadata };

            using MemoryStream reference = new();
            expected.Save(reference, encoder);

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);

            ImageInfo info = Image.Identify(encoded.ToArray());
            using Image<Rgba32> expectedSamples = Image.Load<Rgba32>(reference.ToArray());
            using Image<Rgba32> actual = DecodeBmpSamples(encoded.ToArray());
            actual.DebugSave(provider, testOutputDetails: $"{bitsPerPixel}_{skipMetadata}");

            Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());
            Assert.Equal(BmpInfoHeaderType.WinVersion5, info.Metadata.GetBmpMetadata().InfoHeaderType);
            Assert.Equal(bitsPerPixel, info.Metadata.GetBmpMetadata().BitsPerPixel);
            ImageComparer.Exact.VerifySimilarity(expectedSamples, actual);

            using MemoryStream repeated = new();
            source.Save(repeated, encoder);
            Assert.Equal(encoded.ToArray(), repeated.ToArray());

            ImageComparer.Exact.VerifySimilarity(unchanged, source);
            Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
            Assert.Same(imageProfile, source.Metadata.IccProfile);
        }
    }

    /// <summary>
    /// Checks that indexed output transforms its RGB table without changing indices or source metadata.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format, including the direct L8 index path.</typeparam>
    /// <param name="provider">The existing indexed or grayscale fixture.</param>
    /// <param name="bitsPerPixel">The index precision.</param>
    [Theory]
    [WithFile(TestImages.Bmp.Bit8, PixelTypes.Rgba32, BmpBitsPerPixel.Bit1)]
    [WithFile(TestImages.Bmp.Bit8, PixelTypes.Rgba32, BmpBitsPerPixel.Bit2)]
    [WithFile(TestImages.Bmp.Bit8, PixelTypes.Rgba32, BmpBitsPerPixel.Bit4)]
    [WithFile(TestImages.Bmp.Bit8, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    [WithFile(TestImages.Png.Gray4Bpp, PixelTypes.L8, BmpBitsPerPixel.Bit8)]
    public void Encode_IccProfile_TransformsPaletteWithoutChangingIndices<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        IccColorProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        Color[] originalPalette = source.Metadata.GetBmpMetadata().ColorTable?.ToArray();

        BmpEncoder encoder = new() { BitsPerPixel = bitsPerPixel, SkipMetadata = true };
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;

        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);

        using Image<Rgba32> expected = Image.Load<Rgba32>(unprofiled.ToArray());
        IccColorProfile.GetIccProfileForEncoding(profile, PixelColorType.Indexed, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);

        // Quantize first in sRGB, then transform those selected colors. Indices must still identify the same entries.
        converter.Convert(expected);
        source.Metadata.IccProfile = profile;

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);

        byte[] referenceBytes = unprofiled.ToArray();
        byte[] encodedBytes = encoded.ToArray();
        BmpFileHeader referenceHeader = BmpFileHeader.Parse(referenceBytes);
        BmpFileHeader encodedHeader = BmpFileHeader.Parse(encodedBytes);
        ImageInfo info = Image.Identify(encodedBytes);

        Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());
        Assert.Equal(
            referenceBytes.AsSpan(referenceHeader.Offset).ToArray(),
            encodedBytes.AsSpan(encodedHeader.Offset, referenceBytes.Length - referenceHeader.Offset).ToArray());

        using Image<Rgba32> actual = DecodeBmpSamples(encodedBytes);
        actual.DebugSave(provider, testOutputDetails: bitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(expected, actual);

        Assert.Equal(originalPalette, source.Metadata.GetBmpMetadata().ColorTable?.ToArray());
        Assert.Same(profile, source.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks V5 profile offsets for BMP and DIB output that begins inside an existing stream.
    /// </summary>
    /// <param name="provider">The existing RGB fixture.</param>
    /// <param name="skipFileHeader">Whether output is a DIB resource without a BMP file header.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba32, true)]
    public void Encode_IccProfile_UsesDibRelativeOffset(TestImageProvider<Rgba32> provider, bool skipFileHeader)
    {
        using Image<Rgba32> source = provider.GetImage();
        IccColorProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        BmpEncoder encoder = new() { BitsPerPixel = BmpBitsPerPixel.Bit24, SkipFileHeader = skipFileHeader, SkipMetadata = true };

        // Leave one file-header-sized prefix before the resource. Profile offsets must exclude this parent-stream data.
        using MemoryStream encoded = new();
        encoded.SetLength(BmpFileHeader.Size);
        encoded.Position = BmpFileHeader.Size;
        source.Save(encoded, encoder);

        byte[] bytes = encoded.ToArray();
        int infoHeaderPosition = BmpFileHeader.Size + (skipFileHeader ? 0 : BmpFileHeader.Size);
        BmpInfoHeader infoHeader = BmpInfoHeader.ParseV5(bytes.AsSpan(infoHeaderPosition));
        int profilePosition = infoHeaderPosition + infoHeader.ProfileData;

        Assert.Equal(BmpInfoHeader.SizeV5, infoHeader.HeaderSize);
        Assert.Equal(BmpColorSpace.PROFILE_EMBEDDED, infoHeader.CsType);
        Assert.Equal(BmpInfoHeader.SizeV5 + infoHeader.ImageSize, infoHeader.ProfileData);
        Assert.Equal(profile.ToByteArray(), bytes.AsSpan(profilePosition, infoHeader.ProfileSize).ToArray());
        Assert.Equal(bytes.Length, profilePosition + infoHeader.ProfileSize);

        if (!skipFileHeader)
        {
            BmpFileHeader fileHeader = BmpFileHeader.Parse(bytes.AsSpan(BmpFileHeader.Size));
            Assert.Equal(bytes.Length - BmpFileHeader.Size, fileHeader.FileSize);
        }
    }

    /// <summary>
    /// Decodes stored BMP samples without applying the profile a second time.
    /// </summary>
    /// <param name="encoded">An owned copy of V5 encoder output. Its profile header fields are cleared in place.</param>
    /// <returns>The stored samples in the embedded profile's color space.</returns>
    private static Image<Rgba32> DecodeBmpSamples(byte[] encoded)
    {
        // Clear only the profile reference in our copy of encoder output. Leave samples, palette, masks and profile bytes unchanged.
        Span<byte> headerBytes = encoded.AsSpan(BmpFileHeader.Size, BmpInfoHeader.SizeV5);
        BmpInfoHeader header = BmpInfoHeader.ParseV5(headerBytes);
        header.ProfileData = 0;
        header.ProfileSize = 0;
        header.CsType = BmpColorSpace.LCS_sRGB;
        header.WriteV5Header(headerBytes);

        return Image.Load<Rgba32>(encoded);
    }
}
