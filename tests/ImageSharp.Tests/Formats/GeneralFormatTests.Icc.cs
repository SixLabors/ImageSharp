// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats;

public partial class GeneralFormatTests
{
    /// <summary>
    /// Checks default BMP layout selection and ICC handoff from a real RGB-profile PNG.
    /// </summary>
    /// <param name="provider">The PNG with its embedded RGB profile.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    [Theory]
    [WithFile(TestImages.Png.Icc.Perceptual, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Png.Icc.Perceptual, PixelTypes.Rgba64, true)]
    public void Save_ProfiledPngAsBmp_UsesCompatibleLayout(TestImageProvider<Rgba64> provider, bool skipMetadata)
    {
        using Image<Rgba64> source = provider.GetImage(PngDecoder.Instance);
        using Image<Rgba64> unchanged = provider.GetImage(PngDecoder.Instance);
        IccProfile profile = Assert.IsType<IccProfile>(source.Metadata.IccProfile);
        IccProfile frameProfile = source.Frames.RootFrame.Metadata.IccProfile;
        Assert.Equal(IccColorSpaceType.Rgb, profile.Header.DataColorSpace);

        using MemoryStream inferred = new();
        source.Save(inferred, new BmpEncoder { SkipMetadata = skipMetadata });

        // This fixture stores RGB bytes. Automatic metadata conversion must select the same BMP layout as an explicit request.
        using MemoryStream explicitLayout = new();
        source.Save(explicitLayout, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit24, SkipMetadata = skipMetadata });
        Assert.Equal(explicitLayout.ToArray(), inferred.ToArray());

        inferred.Position = 0;
        ImageInfo info = Image.Identify(inferred);
        Assert.Equal(BmpBitsPerPixel.Bit24, info.Metadata.GetBmpMetadata().BitsPerPixel);
        Assert.Equal(BmpInfoHeaderType.WinVersion5, info.Metadata.GetBmpMetadata().InfoHeaderType);
        Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Metadata.IccProfile);
        Assert.Same(frameProfile, source.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks native CMYK selection when saving a profiled TIFF without destination color overrides.
    /// </summary>
    /// <param name="provider">The real CMYK TIFF.</param>
    /// <param name="jpeg">Whether the destination is JPEG rather than TIFF.</param>
    [Theory]
    [WithFile(TestImages.Tiff.Issue3198, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Tiff.Issue3198, PixelTypes.Rgba64, true)]
    public void Save_ProfiledCmykTiff_UsesCompatibleLayout(TestImageProvider<Rgba64> provider, bool jpeg)
    {
        using Image<Rgba64> source = provider.GetImage(TiffDecoder.Instance);
        using Image<Rgba64> unchanged = provider.GetImage(TiffDecoder.Instance);
        IccProfile profile = Assert.IsType<IccProfile>(source.Frames.RootFrame.Metadata.IccProfile);
        IccProfile imageProfile = source.Metadata.IccProfile;
        Assert.Equal(IccColorSpaceType.Cmyk, profile.Header.DataColorSpace);

        IImageEncoder inferredEncoder = jpeg ? new JpegEncoder { SkipMetadata = true } : new TiffEncoder { SkipMetadata = true };
        IImageEncoder explicitEncoder = jpeg
            ? new JpegEncoder { ColorType = JpegColorType.Cmyk, SkipMetadata = true }
            : new TiffEncoder
            {
                PhotometricInterpretation = TiffPhotometricInterpretation.Separated,
                BitsPerPixel = TiffBitsPerPixel.Bit32,
                SkipMetadata = true
            };

        // Compare public save routes. A wrong inferred model would change both the stored components and profile compatibility.
        using MemoryStream inferred = new();
        source.Save(inferred, inferredEncoder);
        using MemoryStream explicitLayout = new();
        source.Save(explicitLayout, explicitEncoder);
        Assert.Equal(explicitLayout.ToArray(), inferred.ToArray());

        inferred.Position = 0;
        ImageInfo info = Image.Identify(inferred);
        Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());

        if (jpeg)
        {
            Assert.Equal(JpegColorType.Cmyk, info.Metadata.GetJpegMetadata().ColorType);
        }
        else
        {
            Assert.Equal(TiffPhotometricInterpretation.Separated, info.FrameMetadataCollection[0].GetTiffMetadata().PhotometricInterpretation);
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(imageProfile, source.Metadata.IccProfile);
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks that an explicit RGB PNG destination omits a CMYK profile without changing working pixels or source metadata.
    /// </summary>
    /// <param name="provider">The real CMYK TIFF.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    [Theory]
    [WithFile(TestImages.Tiff.Issue3198, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Tiff.Issue3198, PixelTypes.Rgba64, true)]
    public void Save_ProfiledCmykTiffAsRgbPng_OmitsIncompatibleProfile(TestImageProvider<Rgba64> provider, bool skipMetadata)
    {
        using Image<Rgba64> source = provider.GetImage(TiffDecoder.Instance);
        IccProfile profile = Assert.IsType<IccProfile>(source.Frames.RootFrame.Metadata.IccProfile);
        IccProfile imageProfile = source.Metadata.IccProfile;
        byte[] exif = source.Frames.RootFrame.Metadata.ExifProfile?.ToByteArray();
        PngEncoder encoder = new() { ColorType = PngColorType.Rgb, BitDepth = PngBitDepth.Bit16, SkipMetadata = skipMetadata };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        encoded.Position = 0;
        using Image<Rgba64> actual = Image.Load<Rgba64>(encoded);

        // Decode already supplies sRGB working pixels. RGB PNG cannot use the retained CMYK profile and must store those pixels unchanged.
        Assert.Null(actual.Metadata.IccProfile);
        Assert.Equal(PngColorType.Rgb, actual.Metadata.GetPngMetadata().ColorType);
        Assert.Equal(PngBitDepth.Bit16, actual.Metadata.GetPngMetadata().BitDepth);
        ImageComparer.Exact.VerifySimilarity(source, actual);

        using MemoryStream repeated = new();
        source.Save(repeated, encoder);
        Assert.Equal(encoded.ToArray(), repeated.ToArray());
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
        Assert.Same(imageProfile, source.Metadata.IccProfile);
        Assert.Equal(exif, source.Frames.RootFrame.Metadata.ExifProfile?.ToByteArray());
    }

    /// <summary>
    /// Checks working-space background transfer from a GIF palette to WebP and back to encoded GIF.
    /// </summary>
    /// <param name="provider">The real animation with palette colors and background disposal.</param>
    [Theory]
    [WithFile(TestImages.Gif.MixedDisposal, PixelTypes.Rgba32P)]
    public void Save_ProfiledAnimation_TransfersWorkingBackgroundBetweenFormats(TestImageProvider<Rgba32P> provider)
    {
        using Image<Rgba32P> source = provider.GetImage(GifDecoder.Instance);
        using Image<Rgba32P> unchanged = provider.GetImage(GifDecoder.Instance);
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        Color background = source.Metadata.GetGifMetadata().GlobalColorTable.Value.Span[0];
        source.Metadata.IccProfile = profile;
        source.Metadata.GetGifMetadata().BackgroundColor = background;

        ColorProfileConverter toDevice = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile
        });

        ColorProfileConverter toWorking = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile
        });

        // The metadata bridge transfers a working color. WebP alone transforms and packs that color for its ANIM header.
        Color storedBackground = toDevice.Convert<Bgra32>(background);
        Assert.NotEqual(background.ToPixel<Bgra32>(), storedBackground.ToPixel<Bgra32>());
        Color expectedBackground = toWorking.Convert<Rgba32>(storedBackground);

        using MemoryStream encoded = new();
        source.Save(encoded, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless, SkipMetadata = true });
        encoded.Position = 0;
        using Image<Rgba32P> webp = Image.Load<Rgba32P>(encoded);
        Color actualBackground = webp.Metadata.GetWebpMetadata().BackgroundColor;
        Assert.Equal(expectedBackground.ToPixel<Rgba32>(), actualBackground.ToPixel<Rgba32>());
        Assert.Equal(profile.ToByteArray(), webp.Metadata.IccProfile.ToByteArray());

        // Include the decoded background in a fixed working palette so GIF can select an exact color before ICC byte packing.
        // This isolates background transfer from nearest-color quantization. Both encoders must resolve metadata themselves.
        // PaletteQuantizer stores Rgba32P colors here, so apply that pixel rounding before calculating the expected ICC result.
        // Exact matching is required: the default coarse matcher can select a nearby entry even when this color is present.
        Color paletteBackground = Color.FromPixel(actualBackground.ToPixel<Rgba32P>());
        Color[] palette = [paletteBackground, .. source.Metadata.GetGifMetadata().GlobalColorTable.Value.Span];
        GifEncoder gifEncoder = new()
        {
            Quantizer = new PaletteQuantizer(palette, new QuantizerOptions { Dither = null, ColorMatchingMode = ColorMatchingMode.Exact }),
            SkipMetadata = true
        };

        using MemoryStream gifEncoded = new();
        webp.Save(gifEncoded, gifEncoder);
        gifEncoded.Position = 0;
        using Image<Rgba32P> gif = Image.Load<Rgba32P>(gifEncoded);

        Color expectedGifBackground = toWorking.Convert<Rgba32>(toDevice.Convert<Rgb24>(paletteBackground));
        Assert.Equal(expectedGifBackground.ToPixel<Rgba32>(), gif.Metadata.GetGifMetadata().BackgroundColor.Value.ToPixel<Rgba32>());
        Assert.Equal(profile.ToByteArray(), gif.Metadata.IccProfile.ToByteArray());

        // Returning to a palette-backed format must transfer the decoded working color, not the profile's stored ANIM bytes.
        Assert.Equal(actualBackground, webp.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(background, source.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Same(profile, source.Metadata.IccProfile);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks that a converter setup failure does not alter source pixels or replace the supplied profile.
    /// </summary>
    /// <param name="provider">The real RGB image.</param>
    [Theory]
    [WithFile(TestImages.Png.Icc.Perceptual, PixelTypes.Rgba64)]
    public void Save_UnconvertibleProfile_ThrowsWithoutChangingSource(TestImageProvider<Rgba64> provider)
    {
        using Image<Rgba64> source = provider.GetImage(PngDecoder.Instance);
        using Image<Rgba64> unchanged = provider.GetImage(PngDecoder.Instance);

        // Reuse the existing parser fixture: its RGB/XYZ header is valid, but its unknown tags cannot define a color transform.
        // The encoder must propagate that conversion failure instead of writing working pixels under the unusable profile.
        IccProfile profile = new(IccTestDataProfiles.ProfileRandomArray);
        Assert.True(profile.CheckIsValid());
        source.Metadata.IccProfile = profile;

        using MemoryStream encoded = new();
        Assert.Throws<InvalidIccProfileException>(() => source.Save(encoded, new PngEncoder { ColorType = PngColorType.Rgb }));

        Assert.Same(profile, source.Metadata.IccProfile);
        Assert.Equal(IccTestDataProfiles.ProfileRandomArray, profile.ToByteArray());
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }
}
