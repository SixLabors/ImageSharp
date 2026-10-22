// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

public partial class HeifEncoderTests
{
    /// <summary>
    /// Checks the stored RGB samples before the decoder applies ICC.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real high-depth image.</param>
    /// <param name="bitDepth">The encoded sample precision.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, HeifBitDepth.Bit12)]
    public void Encode_IccProfile_ConvertsNativeSamplesAtSelectedDepth<TPixel>(TestImageProvider<TPixel> provider, HeifBitDepth bitDepth)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();

        // This region contains red, green and blue gradients. Odd dimensions cover SIMD tails and the unpaired chroma row.
        source.Mutate(context => context.Crop(new Rectangle(320, 384, 257, 129)));
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Metadata.IccProfile = CompactSrgbV4Profile.Profile;
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        using Image<TPixel> unchanged = source.Clone();
        using Image<RgbaVector> expected = CreateHeifProfileReference(source, profile);
        HeifEncoder encoder = new()
        {
            BitDepth = bitDepth,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Lossless = true,
            SkipMetadata = true,
            Speed = HeifEncodingSpeed.Level9
        };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();
        using Image<RgbaVector> stored = DecodeStoredAv1Colors(file, provider.Configuration);

        // Lossless identity coding rounds each RGB channel once to the selected depth.
        // The comparer uses 65535 units per channel. Three half-code errors give this per-pixel bound.
        int maximumCode = (1 << (int)bitDepth) - 1;
        int pixelThreshold = (int)Math.Ceiling(3D * ushort.MaxValue / (2D * maximumCode));
        ImageComparer comparer = ImageComparer.Tolerant(0F, pixelThreshold);
        comparer.VerifySimilarity(expected, stored);

        // ROMM RGB must change these sRGB samples by more than the final sample rounding permits.
        Assert.NotEmpty(comparer.CompareImages(source, stored));

        ImageInfo info = Image.Identify(file);
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(info.Metadata.IccProfile).ToByteArray());
        Assert.Equal(bitDepth, info.Metadata.GetHeifMetadata().BitDepth);
        Assert.Equal(CicpColorPrimaries.Unspecified, info.Metadata.CicpProfile.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.Unspecified, info.Metadata.CicpProfile.TransferCharacteristics);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
        Assert.Same(CompactSrgbV4Profile.Profile, source.Metadata.IccProfile);
        source.DebugSave(provider, testOutputDetails: $"{bitDepth}", extension: "avif", encoder: encoder);
    }

    /// <summary>
    /// Checks ICC conversion and retention for both primary-item choices of an image sequence.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real animation with alpha.</param>
    /// <param name="animateRootFrame">Whether the first track sample also supplies the primary image.</param>
    [Theory]
    [WithFile(TestImages.Gif.Leo, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, false)]
    [WithFile(TestImages.Gif.Leo, PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, true)]
    public void Encode_IccSequence_UsesSameProfileForPrimaryAndTrack<TPixel>(TestImageProvider<TPixel> provider, bool animateRootFrame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();

        // Two real frames cover the primary item and color track without encoding the complete animation.
        while (source.Frames.Count > 2)
        {
            source.Frames.RemoveFrame(2);
        }

        IccProfile trackProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile primaryProfile = CompactSrgbV4Profile.Profile;
        source.Metadata.IccProfile = trackProfile;
        source.Frames.RootFrame.Metadata.IccProfile = primaryProfile;
        IccProfile profile = animateRootFrame ? primaryProfile : trackProfile;
        using Image<TPixel> unchanged = source.Clone();
        using Image<RgbaVector> reference = CreateHeifProfileReference(source, profile);
        if (!animateRootFrame)
        {
            source.Frames.RootFrame.CopyPixelsTo(reference.Frames.RootFrame.PixelBuffer);
        }

        HeifEncoder encoder = new()
        {
            AnimateRootFrame = animateRootFrame,
            BitDepth = HeifBitDepth.Bit8,
            ChromaSubsampling = HeifChromaSubsampling.Yuv444,
            Lossless = true,
            SkipMetadata = true,
            Speed = HeifEncodingSpeed.Level9
        };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        using MemoryStream referenceStream = new();
        reference.Save(referenceStream, encoder);
        referenceStream.Position = 0;
        using Image<RgbaVector> expected = Image.Load<RgbaVector>(referenceStream);

        // Retain the decoded sRGB primary while the whole-image reference converter processes the track frames.
        using Image<RgbaVector> primary = animateRootFrame ? null : expected.Frames.CloneFrame(0);
        ColorConversionOptions options = new()
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        };

        ColorProfileConverter converter = new(options);
        if (IccProfile.TryGetIccProfileForColorConversion(profile, out _))
        {
            converter.Convert(expected);
        }

        if (primary is not null)
        {
            primary.Frames.RootFrame.CopyPixelsTo(expected.Frames.RootFrame.PixelBuffer);
        }

        using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded.ToArray());

        ImageComparer.Exact.VerifySimilarity(expected, actual);
        Assert.Equal(source.Frames.Count, actual.Frames.Count);
        Assert.Equal(primaryProfile.ToByteArray(), Assert.IsType<IccProfile>(actual.Metadata.IccProfile).ToByteArray());
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(actual.Frames[1].Metadata.IccProfile).ToByteArray());
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(trackProfile, source.Metadata.IccProfile);
        Assert.Same(primaryProfile, source.Frames.RootFrame.Metadata.IccProfile);
        source.DebugSave(provider, testOutputDetails: $"{animateRootFrame}", extension: "avif", encoder: encoder);
    }

    /// <summary>
    /// Checks that lossy and layered encoding apply the return transform and retain its profile.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real high-depth image.</param>
    /// <param name="layered">Whether the encoder writes two spatial layers.</param>
    /// <param name="matrix">The native coding matrix in the source metadata.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, false, CicpMatrixCoefficients.ItuRBt601_7_525)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, true, CicpMatrixCoefficients.ItuRBt601_7_525)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector, false, CicpMatrixCoefficients.ChromaDerivedNcl)]
    public void Encode_IccProfile_RetainsProfileForLossyAndLayeredOutput<TPixel>(
        TestImageProvider<TPixel> provider,
        bool layered,
        CicpMatrixCoefficients matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();

        // Use the same colored region as the lossless test, including SIMD tails and an unpaired chroma row.
        source.Mutate(context => context.Crop(new Rectangle(320, 384, 257, 129)));
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Metadata.IccProfile = profile;

        // The pixels contain working sRGB. The metadata selects the native coding matrix.
        source.Metadata.CicpProfile = new CicpProfile(
            (byte)CicpColorPrimaries.ItuRBt709_6,
            (byte)CicpTransferCharacteristics.Iec61966_2_1,
            (byte)matrix,
            true);

        using Image<TPixel> unchanged = source.Clone();
        HeifEncoder encoder = new()
        {
            BitDepth = HeifBitDepth.Bit10,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420,
            Quality = 80,
            SkipMetadata = true,
            Speed = HeifEncodingSpeed.Level9,
            Layers = layered ? [new HeifLayer { Scale = HeifLayerScale.Half }, new HeifLayer()] : null
        };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();
        CicpProfile codingProfile = Image.Identify(file).Metadata.CicpProfile;

        // A chromaticity-derived matrix cannot use unspecified ICC primaries. The fixed matrix remains fully described.
        Assert.Equal(CicpColorPrimaries.Unspecified, codingProfile.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.Unspecified, codingProfile.TransferCharacteristics);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, codingProfile.MatrixCoefficients);
        using Image<RgbaVector> raw = DecodeStoredAv1Colors(file, provider.Configuration);
        using Image<TPixel> withoutProfile = source.Clone();
        withoutProfile.Metadata.IccProfile = null;
        foreach (ImageFrame<TPixel> frame in withoutProfile.Frames)
        {
            frame.Metadata.IccProfile = null;
        }

        using MemoryStream unconverted = new();
        withoutProfile.Save(unconverted, encoder);
        using Image<RgbaVector> unconvertedRaw = DecodeStoredAv1Colors(unconverted.ToArray(), provider.Configuration);

        // ROMM RGB changes the stored color samples. This also checks that each spatial layer receives the converter.
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(unconvertedRaw, raw));
        DecoderOptions preserveOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve };
        DecoderOptions convertOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert };

        using Image<RgbaVector> preserved = Image.Load<RgbaVector>(preserveOptions, file);
        using Image<RgbaVector> converted = Image.Load<RgbaVector>(convertOptions, file);
        ImageComparer.Exact.VerifySimilarity(preserved, converted);
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(preserved.Metadata.IccProfile).ToByteArray());
        Assert.Null(converted.Metadata.IccProfile);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Metadata.IccProfile);
        source.DebugSave(provider, testOutputDetails: $"{layered}_{matrix}", extension: "avif", encoder: encoder);
    }

    /// <summary>
    /// Checks that an RGB profile is omitted when the encoder writes monochrome samples.
    /// </summary>
    /// <param name="provider">The real RGB image.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.RgbaVector)]
    public void Encode_IccProfile_OmitsIncompatibleMonochromeProfile(TestImageProvider<RgbaVector> provider)
    {
        using Image<RgbaVector> source = provider.GetImage();

        // Use colored source pixels so the monochrome comparison covers conversion to grayscale.
        source.Mutate(context => context.Crop(new Rectangle(320, 384, 257, 129)));
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Metadata.IccProfile = profile;
        using Image<RgbaVector> reference = source.Clone();
        reference.Metadata.IccProfile = null;
        HeifEncoder encoder = new()
        {
            BitDepth = HeifBitDepth.Bit10,
            ChromaSubsampling = HeifChromaSubsampling.Monochrome,
            Lossless = true,
            SkipMetadata = true,
            Speed = HeifEncodingSpeed.Level9
        };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        using MemoryStream referenceStream = new();
        reference.Save(referenceStream, encoder);

        // RGB ICC profiles cannot describe one-channel output. Omitting the profile must leave the existing gray encoding unchanged.
        Assert.Equal(referenceStream.ToArray(), encoded.ToArray());
        Assert.Null(Image.Identify(encoded.ToArray()).Metadata.IccProfile);
        Assert.Same(profile, source.Metadata.IccProfile);
    }

    /// <summary>
    /// Creates a float reference in the output profile with no profile attached.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="source">The working sRGB image.</param>
    /// <param name="profile">The output RGB profile.</param>
    /// <returns>An owned image of device RGB samples.</returns>
    private static Image<RgbaVector> CreateHeifProfileReference<TPixel>(Image<TPixel> source, IccProfile profile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Float storage retains the ICC result until the AV1 writer rounds its native samples.
        Image<RgbaVector> reference = source.CloneAs<RgbaVector>();
        ColorConversionOptions options = new()
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = source.Configuration.MemoryAllocator
        };

        ColorProfileConverter converter = new(options);

        // A canonical sRGB profile requires no color transform.
        if (IccProfile.TryGetIccProfileForColorConversion(profile, out _))
        {
            converter.Convert(reference);
        }

        reference.Metadata.IccProfile = null;
        reference.Metadata.CicpProfile = new CicpProfile(
            (byte)CicpColorPrimaries.Unspecified,
            (byte)CicpTransferCharacteristics.Unspecified,
            (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
            true);

        foreach (ImageFrame<RgbaVector> frame in reference.Frames)
        {
            frame.Metadata.IccProfile = null;
        }

        return reference;
    }

    /// <summary>
    /// Decodes the primary AV1 color item without the container ICC profile.
    /// </summary>
    /// <param name="file">The encoded AVIF file.</param>
    /// <param name="configuration">The configuration used for decoding.</param>
    /// <returns>An owned float image of the stored device RGB samples.</returns>
    private static Image<RgbaVector> DecodeStoredAv1Colors(byte[] file, Configuration configuration)
    {
        using Av1Decoder decoder = new(configuration);
        using Av1FrameBuffer<byte> planes = decoder.DecodeFrameBuffer(GetItemPayload(file, 1), null, null, out _, layeredImageIndex: null);
        Image<RgbaVector> image = new(configuration, planes.Width, planes.Height);
        Av1YuvConverter.ConvertToRgb(
            configuration,
            planes,
            image.Bounds,
            image.Frames.RootFrame.PixelBuffer.GetRegion(image.Bounds),
            image.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            planes.ColorConfig.ColorRange);

        return image;
    }
}
