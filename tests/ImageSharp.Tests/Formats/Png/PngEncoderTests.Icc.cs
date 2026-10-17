// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.


using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

public partial class PngEncoderTests
{
    /// <summary>
    /// Checks that incompatible profiles are omitted and samples match unprofiled encoding.
    /// </summary>
    /// <param name="provider">The existing RGB and alpha fixture.</param>
    /// <param name="colorType">The requested PNG sample layout.</param>
    /// <param name="profileFile">The real ICC profile with an incompatible data color space.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba64, PngColorType.Grayscale, TestIccProfiles.RommRgb)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba64, PngColorType.GrayscaleWithAlpha, TestIccProfiles.RommRgb)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba64, PngColorType.RgbWithAlpha, TestIccProfiles.Fogra39)]
    public void Encode_IncompatibleIccProfile_OmitsProfileWithoutConvertingSamples(
        TestImageProvider<Rgba64> provider, PngColorType colorType, string profileFile)
    {
        using Image<Rgba64> source = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(profileFile);

        foreach (PngInterlaceMode interlace in InterlaceMode)
        {
            foreach (PngBitDepth bitDepth in new[] { PngBitDepth.Bit8, PngBitDepth.Bit16 })
            {
                PngEncoder encoder = new() { ColorType = colorType, BitDepth = bitDepth, InterlaceMethod = interlace };

                // PNG cannot embed an RGB profile with grayscale samples or a CMYK profile with RGB samples.
                // Without a compatible profile, encoding must use the same samples as unprofiled output.
                source.Metadata.IccProfile = null;
                source.Frames.RootFrame.Metadata.IccProfile = null;

                using MemoryStream unprofiled = new();
                source.Save(unprofiled, encoder);

                using Image<Rgba64> expected = Image.Load<Rgba64>(unprofiled.ToArray());

                source.Metadata.IccProfile = profile;
                source.Frames.RootFrame.Metadata.IccProfile = profile;

                using MemoryStream encoded = new();
                source.Save(encoded, encoder);

                ImageInfo info = Image.Identify(encoded.ToArray());
                using Image<Rgba64> actual = Image.Load<Rgba64>(encoded.ToArray());
                actual.DebugSave(provider, testOutputDetails: $"{colorType}_{bitDepth}_{interlace}");

                Assert.Null(info.Metadata.IccProfile);
                Assert.Equal(colorType, info.Metadata.GetPngMetadata().ColorType);
                Assert.Equal(bitDepth, info.Metadata.GetPngMetadata().BitDepth);

                ImageComparer.Exact.VerifySimilarity(expected, actual);

                Assert.Same(profile, source.Metadata.IccProfile);
                Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
            }
        }
    }

    /// <summary>
    /// Checks ICC conversion through normal PNG decoding without changing the source.
    /// </summary>
    /// <typeparam name="TPixel">The sample precision.</typeparam>
    /// <param name="provider">The existing RGB or alpha fixture.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32)]
    public void Encode_IccProfile_TransformsSamplesWithoutChangingSource<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        using Image<TPixel> expected = provider.GetImage();
        using Image<TPixel> unchanged = provider.GetImage();

        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile imageProfile = source.Metadata.IccProfile;

        // A container frame's profile takes precedence over the image profile. The pixels remain in the sRGB working space.
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        source.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true); // These values describe sRGB and must not override ROMM RGB output.
        source.Metadata.GetPngMetadata().Gamma = 1F;

        IccProfile.GetIccProfileForEncoding(profile, PixelColorType.RGB, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);

        converter.Convert(expected);

        // Whole-image conversion rounds to the same eight- or sixteen-bit samples as PNG encoding.
        // Convert those reference samples back to sRGB so the comparison uses normal PNG decoding.
        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        decodeConverter.Convert(expected);

        foreach (PngInterlaceMode interlace in InterlaceMode)
        {
            foreach (bool skipMetadata in new[] { false, true })
            {
                PngEncoder encoder = new()
                {
                    ColorType = PngColorType.RgbWithAlpha,
                    BitDepth = TPixel.GetPixelTypeInfo().ComponentInfo.Value.GetMaximumComponentPrecision() == 16 ? PngBitDepth.Bit16 : PngBitDepth.Bit8,
                    InterlaceMethod = interlace,
                    SkipMetadata = skipMetadata,
                };

                using MemoryStream encoded = new();
                source.Save(encoded, encoder);

                ImageInfo info = Image.Identify(encoded.ToArray());
                using Image<TPixel> actual = Image.Load<TPixel>(encoded.ToArray());
                source.DebugSave(provider, testOutputDetails: $"{interlace}_{skipMetadata}", extension: "png", encoder: encoder);

                Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());

                // ICC describes the encoded colors. Gamma and CICP metadata must not supply a conflicting description.
                Assert.Equal(0F, info.Metadata.GetPngMetadata().Gamma);
                Assert.Null(info.Metadata.CicpProfile);

                ImageComparer.Exact.VerifySimilarity(expected, actual);

                using MemoryStream repeated = new();
                source.Save(repeated, encoder);
                Assert.Equal(encoded.ToArray(), repeated.ToArray());

                ImageComparer.Exact.VerifySimilarity(unchanged, source);
                Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
                Assert.Same(imageProfile, source.Metadata.IccProfile);
                Assert.Equal(1F, source.Metadata.GetPngMetadata().Gamma);
            }
        }
    }

    /// <summary>
    /// Checks that indexed encoding transforms palette colors while keeping indices and transparency unchanged.
    /// </summary>
    /// <param name="provider">The existing indexed PNG fixture.</param>
    [Theory]
    [WithFile(TestImages.Png.Palette8Bpp, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.TestPattern31x31HalfTransparent, PixelTypes.Rgba32)]
    public void Encode_IccProfile_TransformsPalette(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> source = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        Color[] originalPalette = source.Metadata.GetPngMetadata().ColorTable?.ToArray();

        PngEncoder encoder = new() { ColorType = PngColorType.Palette, BitDepth = PngBitDepth.Bit8, SkipMetadata = true };

        // Quantize first in the working space. The same quantizer and table then produce the same indices with an ICC profile present.
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;

        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);

        using Image<Rgba32> expected = Image.Load<Rgba32>(unprofiled.ToArray());

        IccProfile.GetIccProfileForEncoding(profile, PixelColorType.Indexed, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);
        Assert.NotNull(converter);

        converter.Convert(expected);

        // Palette entries are rounded to eight bits before the decoder returns them to sRGB.
        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        decodeConverter.Convert(expected);
        source.Metadata.IccProfile = profile;

        foreach (PngInterlaceMode interlace in InterlaceMode)
        {
            encoder = new PngEncoder { ColorType = PngColorType.Palette, BitDepth = PngBitDepth.Bit8, InterlaceMethod = interlace, SkipMetadata = true };

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);

            ImageInfo info = Image.Identify(encoded.ToArray());
            using Image<Rgba32> actual = Image.Load<Rgba32>(encoded.ToArray());
            source.DebugSave(provider, testOutputDetails: interlace, extension: "png", encoder: encoder);

            Assert.Equal(profile.ToByteArray(), info.Metadata.IccProfile.ToByteArray());
            ImageComparer.Exact.VerifySimilarity(expected, actual);

            Assert.Equal(originalPalette, source.Metadata.GetPngMetadata().ColorTable?.ToArray());

            using MemoryStream repeated = new();
            source.Save(repeated, encoder);
            Assert.Equal(encoded.ToArray(), repeated.ToArray());
        }
    }

    /// <summary>
    /// Checks that APNG retains sixteen-bit samples and changes smaller than one eight-bit code.
    /// </summary>
    /// <param name="provider">The existing sixteen-bit PNG fixture.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64)]
    public void Encode_APng_PreservesSixteenBitDifferences(TestImageProvider<Rgba64> provider)
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunSixteenBitAnimation, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic, provider);
    }

    /// <summary>
    /// Runs APNG precision checks with the selected hardware features.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    private static void RunSixteenBitAnimation(string providerDump)
    {
        TestImageProvider<Rgba64> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba64>>(providerDump);
        using Image<Rgba64> source = provider.GetImage();
        ImageFrame<Rgba64> second = source.Frames.AddFrame(source.Frames.RootFrame);

        // One sixteen-bit code is smaller than an eight-bit step (257 codes). Pick a channel away from an eight-bit rounding boundary.
        Rgba64 pixel = second[second.Width / 2, second.Height / 2];
        pixel.R = (ushort)((pixel.R / 257 * 257) + 1);
        source.Frames.RootFrame[second.Width / 2, second.Height / 2] = pixel;
        pixel.R++;
        second[second.Width / 2, second.Height / 2] = pixel;

        Assert.Equal(source.Frames.RootFrame[second.Width / 2, second.Height / 2].ToRgba32(), pixel.ToRgba32());

        second.Metadata.GetPngMetadata().BlendMode = FrameBlendMode.Over;

        foreach (PngInterlaceMode interlace in InterlaceMode)
        {
            PngEncoder encoder = new() { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit16, InterlaceMethod = interlace };

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);

            using Image<Rgba64> actual = Image.Load<Rgba64>(encoded.ToArray());
            actual.DebugSave(provider, testOutputDetails: interlace);

            ImageComparer.Exact.VerifySimilarity(source, actual);
        }
    }


}
