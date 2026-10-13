// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

public partial class WebpEncoderTests
{
    /// <summary>
    /// Compares row conversion with whole-image conversion before WebP compression and checks source preservation.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real alpha or high-depth image.</param>
    /// <param name="fileFormat">The compression format.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossless, false)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossless, true)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossy, false)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossy, true)]
    [WithFile(TestImages.Webp.Lossy.WithIccpAndAlpha, PixelTypes.Rgba32P, WebpFileFormatType.Lossless, true)]
    [WithFile(TestImages.Webp.Lossy.WithIccpAndAlpha, PixelTypes.Rgba32P, WebpFileFormatType.Lossy, true)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, WebpFileFormatType.Lossless, true)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, WebpFileFormatType.Lossy, true)]
    public void Encode_IccProfile_TransformsSamplesWithoutChangingSource<TPixel>(
        TestImageProvider<TPixel> provider,
        WebpFileFormatType fileFormat,
        bool skipMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();

        // Crop real pixels to an odd height so lossy encoding must transform its unpaired final row.
        int height = source.Height % 2 == 0 ? source.Height - 1 : source.Height;
        source.Mutate(context => context.Crop(new Rectangle(0, 0, source.Width, height)));

        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile imageProfile = source.Metadata.IccProfile;
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        using Image<TPixel> unchanged = source.Clone();
        using Image<RgbaVector> expected = CreateProfileReference(source, profile);
        WebpEncoder encoder = new()
        {
            FileFormat = fileFormat,
            SkipMetadata = skipMetadata,
            TransparentColorMode = TransparentColorMode.Preserve
        };

        using MemoryStream reference = new();
        expected.Save(reference, encoder);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);

        ImageInfo info = Image.Identify(encoded.ToArray());
        using Image<RgbaVector> expectedPixels = DecodeProfileReference(reference, profile, provider.Configuration);
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded.ToArray());

        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(info.Metadata.IccProfile).ToByteArray());
        ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);
        source.DebugSave(provider, testOutputDetails: $"{fileFormat}_{skipMetadata}", extension: "webp", encoder: encoder);

        using MemoryStream repeated = new();
        source.Save(repeated, encoder);

        Assert.Equal(encoded.ToArray(), repeated.ToArray());
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
        Assert.Same(imageProfile, source.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks profile conversion for animation frames and retained or overridden disposal backgrounds.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real ICC animation.</param>
    /// <param name="fileFormat">The compression format.</param>
    /// <param name="overrideBackground">Whether the encoder overrides the metadata background.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossy.AnimatedWithIccp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossless, false)]
    [WithFile(TestImages.Webp.Lossy.AnimatedWithIccp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossless, true)]
    [WithFile(TestImages.Webp.Lossy.AnimatedWithIccp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossy, false)]
    [WithFile(TestImages.Webp.Lossy.AnimatedWithIccp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, WebpFileFormatType.Lossy, true)]
    public void Encode_IccAnimation_UsesResolvedBackgroundWithoutChangingSource<TPixel>(
        TestImageProvider<TPixel> provider,
        WebpFileFormatType fileFormat,
        bool overrideBackground)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        IccProfile profile = Assert.IsType<IccProfile>(source.Metadata.IccProfile);

        // Distinct colors reveal precedence and ICC direction. Partial alpha must survive ANIM byte packing.
        Color metadataBackground = Color.Red.WithAlpha(.5F);
        Color? encoderBackground = overrideBackground ? Color.Blue.WithAlpha(.5F) : null;
        Color background = encoderBackground ?? metadataBackground;
        source.Metadata.GetWebpMetadata().BackgroundColor = metadataBackground;
        foreach (ImageFrame<TPixel> frame in source.Frames)
        {
            frame.Metadata.GetWebpMetadata().DisposalMode = FrameDisposalMode.RestoreToBackground;
        }

        using Image<TPixel> unchanged = source.Clone();
        using Image<RgbaVector> expected = CreateProfileReference(source, profile);
        IccProfile.GetIccProfileForEncoding(profile, PixelColorType.RGB, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);

        // The profile-free reference stores device colors, including the same resolved ANIM background.
        expected.Metadata.GetWebpMetadata().BackgroundColor = converter.Convert<Bgra32>(background);
        WebpEncoder referenceEncoder = new()
        {
            FileFormat = fileFormat,
            SkipMetadata = true,
            TransparentColorMode = TransparentColorMode.Preserve
        };

        WebpEncoder encoder = new()
        {
            FileFormat = fileFormat,
            BackgroundColor = encoderBackground,
            SkipMetadata = true,
            TransparentColorMode = TransparentColorMode.Preserve
        };

        using MemoryStream reference = new();
        expected.Save(reference, referenceEncoder);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        using Image<RgbaVector> expectedPixels = DecodeProfileReference(reference, profile, provider.Configuration);
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded.ToArray());

        Assert.Equal(source.Frames.Count, actual.Frames.Count);
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(actual.Metadata.IccProfile).ToByteArray());
        Assert.Equal(background.ToPixel<Rgba32>(), actual.Metadata.GetWebpMetadata().BackgroundColor.ToPixel<Rgba32>());
        ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);
        source.DebugSave(provider, testOutputDetails: $"{fileFormat}_{overrideBackground}", extension: "webp", encoder: encoder);

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Metadata.IccProfile);
        Assert.Equal(metadataBackground, source.Metadata.GetWebpMetadata().BackgroundColor);
    }

    /// <summary>
    /// Creates a floating reference with whole-image ICC conversion before final WebP byte packing.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="source">The working sRGB image, which remains unchanged.</param>
    /// <param name="profile">The output RGB profile.</param>
    /// <returns>An owned device-space image without ICC metadata.</returns>
    private static Image<RgbaVector> CreateProfileReference<TPixel>(Image<TPixel> source, IccProfile profile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Floating storage avoids an extra quantization back into the source pixel format before BGRA packing.
        Image<RgbaVector> expected = source.CloneAs<RgbaVector>();
        IccProfile.GetIccProfileForEncoding(profile, PixelColorType.RGB, source.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);
        converter.Convert(expected);
        expected.Metadata.IccProfile = null;
        foreach (ImageFrame<RgbaVector> frame in expected.Frames)
        {
            frame.Metadata.IccProfile = null;
        }

        return expected;
    }

    /// <summary>
    /// Decodes the profile-free reference and converts its stored device colors back to working sRGB.
    /// </summary>
    /// <param name="stream">The compressed device-space reference.</param>
    /// <param name="profile">The profile describing the stored device colors.</param>
    /// <param name="configuration">The test configuration.</param>
    /// <returns>An owned sRGB reference image.</returns>
    private static Image<RgbaVector> DecodeProfileReference(MemoryStream stream, IccProfile profile, Configuration configuration)
    {
        stream.Position = 0;
        Image<RgbaVector> expected = Image.Load<RgbaVector>(stream);
        ColorConversionOptions options = new()
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = configuration.MemoryAllocator
        };

        ColorProfileConverter converter = new(options);
        converter.Convert(expected);
        return expected;
    }
}
