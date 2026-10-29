// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Gif;

public partial class GifEncoderTests
{
    /// <summary>
    /// Checks appearance and profile retention after a real ICC GIF is decoded with Preserve and encoded again.
    /// </summary>
    /// <typeparam name="TPixel">The decoded pixel format.</typeparam>
    /// <param name="provider">The GIF with an embedded Adobe RGB profile.</param>
    /// <param name="colorTableMode">The output color-table mode.</param>
    [Theory]
    [WithFile(TestImages.Gif.AdobeRgbIcc, PixelTypes.Rgba32 | PixelTypes.RgbaVector, FrameColorTableMode.Global)]
    [WithFile(TestImages.Gif.AdobeRgbIcc, PixelTypes.Rgba32 | PixelTypes.RgbaVector, FrameColorTableMode.Local)]
    public void Encode_IccProfile_PreserveRoundTrip<TPixel>(TestImageProvider<TPixel> provider, FrameColorTableMode colorTableMode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new(provider.Configuration)
        {
            ColorProfileHandling = ColorProfileHandling.Preserve,
            SkipMetadata = true
        };

        DecoderOptions convertOptions = new(provider.Configuration)
        {
            ColorProfileHandling = ColorProfileHandling.Convert,
            SkipMetadata = true
        };

        using Image<TPixel> preserved = provider.GetImage(GifDecoder.Instance, preserveOptions);
        using Image<TPixel> expected = provider.GetImage(GifDecoder.Instance, convertOptions);
        IccProfile originalProfile = Assert.IsType<IccProfile>(preserved.Metadata.IccProfile);
        byte[] originalProfileData = originalProfile.ToByteArray();
        ImageComparer.Exact.VerifySimilarity(expected, preserved);
        Assert.Null(expected.Metadata.IccProfile);

        // Reuse the decoded sRGB palette with exact matching. Coarse caching can select neighboring palette entries;
        // this test measures ICC round-trip error without additional palette selection error or dithering.
        QuantizerOptions quantizerOptions = new() { Dither = null, ColorMatchingMode = ColorMatchingMode.Exact };
        GifEncoder encoder = new()
        {
            ColorTableMode = colorTableMode,
            Quantizer = new PaletteQuantizer(preserved.Metadata.GetGifMetadata().GlobalColorTable.Value, quantizerOptions),
            SkipMetadata = true
        };

        using MemoryStream encoded = new();
        preserved.Save(encoded, encoder);
        byte[] file = encoded.ToArray();
        IccProfile encodedProfile = Assert.IsType<IccProfile>(Image.Identify(preserveOptions, file).Metadata.IccProfile);
        Assert.Equal(originalProfileData, encodedProfile.ToByteArray());
        Assert.Equal(originalProfileData, preserved.Metadata.IccProfile.ToByteArray());
        ImageComparer.Exact.VerifySimilarity(expected, preserved);

        using Image<TPixel> actual = Image.Load<TPixel>(convertOptions, file);

        Assert.Null(actual.Metadata.IccProfile);

        preserved.DebugSave(provider, testOutputDetails: $"{colorTableMode}_Icc", extension: "gif", encoder: encoder);

        // GIF rounds palette RGB to eight bits. Allow a total error of three eight-bit codes per pixel after the ICC round trip.
        // The comparer uses sixteen-bit units: 65535 / 255 = 257. No pixel may exceed this bound.
        ImageComparer.Tolerant(0F, 3 * 257).VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Checks palette return conversion, ICC subblocks and source preservation with both color-table modes.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The existing opaque GIF fixture.</param>
    /// <param name="colorTableMode">The output color-table mode.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    [Theory]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, FrameColorTableMode.Global, true)]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, FrameColorTableMode.Local, false)]
    public async Task Encode_IccProfile_TransformsPalettesWithoutChangingSource<TPixel>(
        TestImageProvider<TPixel> provider,
        FrameColorTableMode colorTableMode,
        bool skipMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        GifEncoder encoder = new() { ColorTableMode = colorTableMode, SkipMetadata = skipMetadata };
        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);

        // GIF stores eight-bit RGB. Convert the already-quantized reference so palette selection remains in sRGB.
        using Image<Rgba32> expected = Image.Load<Rgba32>(unprofiled.ToArray());
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile
        });

        converter.Convert(expected);

        // GIF stores rounded eight-bit palette entries. Expand those entries to TPixel before the reference ICC decode,
        // so float destinations retain the sRGB result without another eight-bit rounding step.
        using Image<TPixel> expectedPixels = expected.CloneAs<TPixel>();
        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile
        });

        decodeConverter.Convert(expectedPixels);
        source.Metadata.IccProfile = CompactSrgbV4Profile.Profile;
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        using Image<TPixel> unchanged = source.Clone();
        Color[] globalPalette = source.Metadata.GetGifMetadata().GlobalColorTable?.ToArray();
        Color[] localPalette = source.Frames.RootFrame.Metadata.GetGifMetadata().LocalColorTable?.ToArray();

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();
        using Image<TPixel> stored = Image.Load<TPixel>(file);

        // Check the complete encoded file through the decoder API, including the selected root-frame profile.
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(stored.Metadata.IccProfile).ToByteArray());

        // Whole-image and palette conversion can differ within the measured curve approximation bound.
        // Eight-bit output must still match exactly. For float output, add the three RGB error bounds to set the per-pixel limit.
        ImageComparer comparer = TPixel.GetPixelTypeInfo().ComponentInfo.Value.GetMaximumComponentPrecision() <= 8
            ? ImageComparer.Exact
            : ImageComparer.Tolerant(0F, (int)MathF.Ceiling(3F * ushort.MaxValue * 5E-4F));
        comparer.VerifySimilarity(expectedPixels, stored);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Equal(globalPalette, source.Metadata.GetGifMetadata().GlobalColorTable?.ToArray());
        Assert.Equal(localPalette, source.Frames.RootFrame.Metadata.GetGifMetadata().LocalColorTable?.ToArray());
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
        Assert.Same(CompactSrgbV4Profile.Profile, source.Metadata.IccProfile);

        using MemoryStream repeated = new();
        source.Save(repeated, encoder);
        Assert.Equal(file, repeated.ToArray());

        // This fixture is opaque and has one complete frame, so the independent decoder needs no animation composition.
        encoded.Position = 0;
        MagickReferenceDecoder referenceDecoder = new(GifFormat.Instance);
        using Image<TPixel> independent = await referenceDecoder.DecodeAsync<TPixel>(
            new DecoderOptions(provider.Configuration),
            encoded,
            TestContext.Current.CancellationToken);

        // Independent CMMs use different power evaluation. Bound each pixel by the measured RGB curve error.
        ImageComparer.Tolerant(0F, (int)MathF.Ceiling(3F * ushort.MaxValue * 5E-4F))
            .VerifySimilarity(independent, stored);
        source.DebugSave(provider, testOutputDetails: $"{colorTableMode}_{skipMetadata}", extension: "gif", encoder: encoder);
    }

    /// <summary>
    /// Checks ICC palette and background conversion while preserving animation timing, disposal and source data.
    /// </summary>
    /// <typeparam name="TPixel">The associated eight-bit source format.</typeparam>
    /// <param name="provider">The animation with transparency and background disposal.</param>
    /// <param name="colorTableMode">The output color-table mode.</param>
    [Theory]
    [WithFile(TestImages.Gif.MixedDisposal, PixelTypes.Rgba32P, FrameColorTableMode.Global)]
    [WithFile(TestImages.Gif.MixedDisposal, PixelTypes.Rgba32P, FrameColorTableMode.Local)]
    public void Encode_IccAnimation_PreservesFramesAndBackground<TPixel>(TestImageProvider<TPixel> provider, FrameColorTableMode colorTableMode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        GifEncoder encoder = new()
        {
            ColorTableMode = colorTableMode,
            Quantizer = new PaletteQuantizer(source.Metadata.GetGifMetadata().GlobalColorTable.Value, new QuantizerOptions { Dither = null })
        };

        // The unprofiled encode supplies the expected quantization, transparency, timing, disposal and selected background.
        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);
        using Image<TPixel> expected = Image.Load<TPixel>(unprofiled.ToArray());
        Color? background = expected.Metadata.GetGifMetadata().BackgroundColor;
        Assert.Contains(expected.Frames.AsEnumerable(), frame => frame.Metadata.GetGifMetadata().HasTransparency);
        Assert.Contains(expected.Frames.AsEnumerable(), frame => frame.Metadata.GetGifMetadata().DisposalMode == FrameDisposalMode.RestoreToBackground);

        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        ColorProfileConverter encodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile
        });

        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile
        });

        // GIF alpha is binary. The associated eight-bit reference rounds the profile samples and keeps transparent pixels at zero.
        encodeConverter.Convert(expected);
        decodeConverter.Convert(expected);
        source.Metadata.IccProfile = profile;
        using Image<TPixel> unchanged = source.Clone();

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        using Image<TPixel> actual = Image.Load<TPixel>(encoded.ToArray());

        // Normal decoding checks the complete animation and embedded profile without rewriting the encoded file.
        ImageComparer.Exact.VerifySimilarity(expected, actual);
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(actual.Metadata.IccProfile).ToByteArray());
        Assert.Equal(expected.Metadata.GetGifMetadata().RepeatCount, actual.Metadata.GetGifMetadata().RepeatCount);

        // Each frame must retain the metadata that controls its timing, disposal and transparent palette index.
        for (int i = 0; i < expected.Frames.Count; i++)
        {
            GifFrameMetadata expectedMetadata = expected.Frames[i].Metadata.GetGifMetadata();
            GifFrameMetadata actualMetadata = actual.Frames[i].Metadata.GetGifMetadata();
            Assert.Equal(expectedMetadata.FrameDelay, actualMetadata.FrameDelay);
            Assert.Equal(expectedMetadata.DisposalMode, actualMetadata.DisposalMode);
            Assert.Equal(expectedMetadata.HasTransparency, actualMetadata.HasTransparency);
            Assert.Equal(expectedMetadata.TransparencyIndex, actualMetadata.TransparencyIndex);
        }

        // The background is a global palette entry. It has the same byte rounding and ICC decode as that entry.
        if (colorTableMode == FrameColorTableMode.Global)
        {
            Assert.NotNull(background);
            Color encodedBackground = encodeConverter.Convert<Rgb24>(background.Value);
            Color expectedBackground = decodeConverter.Convert<TPixel>(encodedBackground);
            Assert.Equal(expectedBackground, actual.Metadata.GetGifMetadata().BackgroundColor);
        }
        else
        {
            // GIF has no logical-screen background color without a global table.
            Assert.Null(actual.Metadata.GetGifMetadata().BackgroundColor);
        }

        // Saving must not replace the source pixels, palettes or profile.
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Equal(unchanged.Metadata.GetGifMetadata().GlobalColorTable?.ToArray(), source.Metadata.GetGifMetadata().GlobalColorTable?.ToArray());
        for (int i = 0; i < source.Frames.Count; i++)
        {
            Assert.Equal(
                unchanged.Frames[i].Metadata.GetGifMetadata().LocalColorTable?.ToArray(),
                source.Frames[i].Metadata.GetGifMetadata().LocalColorTable?.ToArray());
        }

        Assert.Same(profile, source.Metadata.IccProfile);
        source.DebugSave(provider, testOutputDetails: colorTableMode, extension: "gif", encoder: encoder);
    }

    /// <summary>
    /// Checks that encoder options override GIF background and repeat-count metadata.
    /// </summary>
    /// <param name="provider">The animation with background disposal.</param>
    [Theory]
    [WithFile(TestImages.Gif.MixedDisposal, PixelTypes.Rgba32P)]
    public void Encode_IccAnimation_EncoderOptionsOverrideMetadata(TestImageProvider<Rgba32P> provider)
    {
        using Image<Rgba32P> source = provider.GetImage();
        ReadOnlyMemory<Color> palette = source.Metadata.GetGifMetadata().GlobalColorTable.Value;
        Color metadataBackground = palette.Span[0];
        Color encoderBackground = palette.Span[^1];
        Assert.NotEqual(metadataBackground, encoderBackground);

        // Different values expose an ignored override. The reference receives the requested values through metadata.
        const ushort metadataRepeatCount = 2;
        const ushort encoderRepeatCount = 5;
        source.Metadata.GetGifMetadata().BackgroundColor = metadataBackground;
        source.Metadata.GetGifMetadata().RepeatCount = metadataRepeatCount;
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Metadata.IccProfile = profile;
        using Image<Rgba32P> reference = source.Clone();
        reference.Metadata.GetGifMetadata().BackgroundColor = encoderBackground;
        reference.Metadata.GetGifMetadata().RepeatCount = encoderRepeatCount;

        PaletteQuantizer quantizer = new(palette, new QuantizerOptions { Dither = null });
        GifEncoder encoder = new() { Quantizer = quantizer, BackgroundColor = encoderBackground, RepeatCount = encoderRepeatCount };
        GifEncoder referenceEncoder = new() { Quantizer = quantizer };
        using MemoryStream expected = new();
        reference.Save(expected, referenceEncoder);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();

        // Explicit options must match equivalent metadata with the reference encoder's options unset.
        Assert.Equal(expected.ToArray(), file);
        ImageInfo info = Image.Identify(file);
        Assert.Equal(encoderRepeatCount, info.Metadata.GetGifMetadata().RepeatCount);
        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(info.Metadata.IccProfile).ToByteArray());

        // Overrides apply to the output, not the source metadata.
        Assert.Equal(metadataBackground, source.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(metadataRepeatCount, source.Metadata.GetGifMetadata().RepeatCount);
        Assert.Same(profile, source.Metadata.IccProfile);
        source.DebugSave(provider, extension: "gif", encoder: encoder);
    }

    /// <summary>
    /// Checks that canonical sRGB keeps normal samples and an incompatible CMYK profile is omitted.
    /// </summary>
    /// <param name="provider">The existing opaque GIF fixture.</param>
    /// <param name="canonical">Whether the selected profile is canonical sRGB rather than CMYK.</param>
    [Theory]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32, false)]
    public void Encode_IccProfile_UsesNormalSamplesForIdentityOrIncompatibleProfile(TestImageProvider<Rgba32> provider, bool canonical)
    {
        using Image<Rgba32> source = provider.GetImage();
        GifEncoder encoder = new() { SkipMetadata = true };
        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);
        using Image<Rgba32> expected = Image.Load<Rgba32>(unprofiled.ToArray());

        IccProfile profile = canonical ? CompactSrgbV4Profile.Profile : TestIccProfiles.GetProfile(TestIccProfiles.Fogra39);
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        source.Metadata.IccProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();

        if (canonical)
        {
            using Image<Rgba32> stored = Image.Load<Rgba32>(file);
            Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(stored.Metadata.IccProfile).ToByteArray());
            ImageComparer.Exact.VerifySimilarity(expected, stored);
        }
        else
        {
            // A CMYK profile cannot describe GIF's RGB tables. Its omission must leave all normal output bytes unchanged.
            Assert.Equal(unprofiled.ToArray(), file);
            Assert.Null(Image.Identify(file).Metadata.IccProfile);
        }

        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
    }
}
