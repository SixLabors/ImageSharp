// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

public partial class TiffEncoderTests
{
    /// <summary>
    /// Checks the return transform at each supported RGB depth, without rounding through the source pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">The real RGB or alpha image.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32)]
    public void Encode_IccProfile_TransformsSamplesWithoutChangingSource<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        using Image<TPixel> unchanged = source.Clone();
        using Image<RgbaVector> expected = source.CloneAs<RgbaVector>();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile originalImageProfile = source.Metadata.IccProfile;
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        // The reference applies ICC before sample packing, using float pixels even for an eight-bit source.
        // This catches an intermediate conversion back to TPixel in the writer.
        ColorProfileConverter encodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        encodeConverter.Convert(expected);
        expected.Metadata.IccProfile = null;
        expected.Frames.RootFrame.Metadata.IccProfile = null;

        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        (TiffBitsPerPixel Depth, TiffSampleFormat Format)[] layouts =
        [
            (TiffBitsPerPixel.Bit24, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit48, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit64, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit96, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit128, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit96, TiffSampleFormat.Float),
            (TiffBitsPerPixel.Bit128, TiffSampleFormat.Float)
        ];

        foreach ((TiffBitsPerPixel depth, TiffSampleFormat format) in layouts)
        {
            foreach (bool skipMetadata in new[] { false, true })
            {
                TiffEncoder encoder = new() { BitsPerPixel = depth, SampleFormat = format, Compression = TiffCompression.Deflate, SkipMetadata = skipMetadata };
                using MemoryStream reference = new();
                expected.Save(reference, encoder);
                reference.Position = 0;
                using Image<RgbaVector> expectedPixels = Image.Load<RgbaVector>(reference);
                decodeConverter.Convert(expectedPixels);

                using MemoryStream encoded = new();
                source.Save(encoded, encoder);
                encoded.Position = 0;
                using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);
                TiffFrameMetadata actualMetadata = actual.Frames.RootFrame.Metadata.GetTiffMetadata();
                if (!skipMetadata)
                {
                    source.DebugSave(provider, testOutputDetails: $"{depth}_{format}", extension: "tiff", encoder: encoder);
                }

                Assert.Equal(profile.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());
                Assert.Equal(depth, actualMetadata.BitsPerPixel);
                Assert.Equal(format, actualMetadata.SampleFormat);
                ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);

                ImageComparer.Exact.VerifySimilarity(unchanged, source);
                Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
                Assert.Same(originalImageProfile, source.Metadata.IccProfile);
            }
        }

    }

    /// <summary>
    /// Checks that a final indexed palette retains 16-bit ICC output instead of using eight-bit palette samples.
    /// </summary>
    /// <param name="provider">The existing indexed image.</param>
    /// <param name="depth">The number of index bits.</param>
    [Theory]
    [WithFile(TestImages.Png.Palette8Bpp, PixelTypes.Rgba32, TiffBitsPerPixel.Bit4)]
    [WithFile(TestImages.Png.Palette8Bpp, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    public void Encode_IccProfile_TransformsFinalPalette(TestImageProvider<Rgba32> provider, TiffBitsPerPixel depth)
    {
        using Image<Rgba32> source = provider.GetImage();
        using Image<Rgba32> unchanged = source.Clone();
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;
        TiffEncoder encoder = new() { BitsPerPixel = depth, PhotometricInterpretation = TiffPhotometricInterpretation.PaletteColor, SkipMetadata = true };

        using MemoryStream unprofiled = new();
        source.Save(unprofiled, encoder);
        unprofiled.Position = 0;
        using Image<RgbaVector> expected = Image.Load<RgbaVector>(unprofiled);
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);

        ColorProfileConverter encodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        // Quantization fixes the sRGB colors and their indices first. ColorMap then rounds the transformed colors to 16 bits.
        encodeConverter.Convert(expected);
        using MemoryStream packed = new();
        expected.Save(packed, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit48 });
        packed.Position = 0;
        using Image<RgbaVector> expectedPixels = Image.Load<RgbaVector>(packed);

        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        decodeConverter.Convert(expectedPixels);
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        encoded.Position = 0;
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);

        Assert.Equal(profile.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());
        Assert.Equal(depth, actual.Frames.RootFrame.Metadata.GetTiffMetadata().BitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks that each IFD resolves its own settings, and explicit encoder settings override every frame.
    /// </summary>
    /// <param name="provider">The real multi-frame TIFF.</param>
    /// <param name="overrideSettings">Whether the encoder supplies explicit settings.</param>
    /// <param name="secondInterpretation">The second frame's sample layout.</param>
    [Theory]
    [WithFile(TestImages.Tiff.MultiframeLzwPredictor, PixelTypes.Rgba32, false, TiffPhotometricInterpretation.WhiteIsZero)]
    [WithFile(TestImages.Tiff.MultiframeLzwPredictor, PixelTypes.Rgba32, true, TiffPhotometricInterpretation.WhiteIsZero)]
    [WithFile(TestImages.Tiff.MultiframeLzwPredictor, PixelTypes.Rgba32, false, TiffPhotometricInterpretation.CieLab)]
    [WithFile(TestImages.Tiff.MultiframeLzwPredictor, PixelTypes.Rgba32, false, TiffPhotometricInterpretation.YCbCr)]
    public void Encode_ResolvesSettingsPerFrame(TestImageProvider<Rgba32> provider, bool overrideSettings, TiffPhotometricInterpretation secondInterpretation)
    {
        using Image<Rgba32> source = provider.GetImage();

        // Give each frame different settings so the encoder cannot reuse the first frame's resolved layout.
        TiffFrameMetadata first = source.Frames[0].Metadata.GetTiffMetadata();
        first.SampleFormat = TiffSampleFormat.Float;
        first.BitsPerPixel = TiffBitsPerPixel.Bit96;
        first.PhotometricInterpretation = TiffPhotometricInterpretation.Rgb;
        first.Compression = TiffCompression.PackBits;
        first.Predictor = TiffPredictor.None;

        TiffFrameMetadata second = source.Frames[1].Metadata.GetTiffMetadata();
        second.SampleFormat = TiffSampleFormat.UnsignedInteger;

        TiffBitsPerPixel secondDepth = secondInterpretation switch
        {
            TiffPhotometricInterpretation.CieLab => TiffBitsPerPixel.Bit48,
            TiffPhotometricInterpretation.YCbCr => TiffBitsPerPixel.Bit24,
            _ => TiffBitsPerPixel.Bit16
        };

        second.BitsPerPixel = secondDepth;
        second.PhotometricInterpretation = secondInterpretation;
        second.Compression = TiffCompression.Lzw;
        second.Predictor = TiffPredictor.Horizontal;

        // Explicit encoder options must replace both frames' settings. Null options must retain each frame's settings.
        TiffEncoder encoder = overrideSettings
            ? new TiffEncoder
            {
                SampleFormat = TiffSampleFormat.UnsignedInteger,
                BitsPerPixel = TiffBitsPerPixel.Bit48,
                PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
                Compression = TiffCompression.Deflate,
                HorizontalPredictor = TiffPredictor.None
            }
            : new TiffEncoder();

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        encoded.Position = 0;
        ImageInfo info = Image.Identify(encoded);

        for (int i = 0; i < 2; i++)
        {
            TiffFrameMetadata expected = source.Frames[i].Metadata.GetTiffMetadata();
            TiffFrameMetadata actual = info.FrameMetadataCollection[i].GetTiffMetadata();

            Assert.Equal(overrideSettings ? TiffSampleFormat.UnsignedInteger : expected.SampleFormat, actual.SampleFormat);
            Assert.Equal(overrideSettings ? TiffBitsPerPixel.Bit48 : expected.BitsPerPixel, actual.BitsPerPixel);
            Assert.Equal(overrideSettings ? TiffPhotometricInterpretation.Rgb : expected.PhotometricInterpretation, actual.PhotometricInterpretation);

            Assert.Equal(overrideSettings ? TiffCompression.Deflate : expected.Compression, actual.Compression);
            Assert.Equal(overrideSettings ? TiffPredictor.None : expected.Predictor, actual.Predictor);
        }

        // Resolving output options must leave the source metadata unchanged.
        Assert.Equal(TiffSampleFormat.Float, first.SampleFormat);
        Assert.Equal(TiffBitsPerPixel.Bit96, first.BitsPerPixel);

        Assert.Equal(TiffSampleFormat.UnsignedInteger, second.SampleFormat);
        Assert.Equal(secondDepth, second.BitsPerPixel);
    }

    /// <summary>
    /// Checks that an unprofiled TIFF frame does not inherit the image's copy of the first IFD profile.
    /// </summary>
    /// <param name="provider">The real multi-frame TIFF.</param>
    [Theory]
    [WithFile(TestImages.Tiff.MultiframeLzwPredictor, PixelTypes.Rgba32)]
    public void Encode_PreservesFrameProfileScope(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> source = provider.GetImage();
        IccProfile firstProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile lastProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV4);
        source.Metadata.IccProfile = firstProfile;
        source.Frames[0].Metadata.IccProfile = firstProfile;
        source.Frames[1].Metadata.IccProfile = null;
        source.Frames.AddFrame(source.Frames[0]);
        source.Frames[^1].Metadata.IccProfile = lastProfile;
        using Image<Rgba32> unchanged = source.Clone();
        using MemoryStream encoded = new();
        source.Save(encoded, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit48, SkipMetadata = true });

        encoded.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(encoded);
        Assert.Equal(firstProfile.ToByteArray(), actual.Frames[0].Metadata.IccProfile.ToByteArray());
        Assert.Null(actual.Frames[1].Metadata.IccProfile);
        Assert.Equal(lastProfile.ToByteArray(), actual.Frames[^1].Metadata.IccProfile.ToByteArray());

        // A single-frame save provides a fresh settings/profile scope. It must match the same frame
        // saved inside the multi-frame file, including the second IFD with no profile.
        TiffEncoder encoder = new() { BitsPerPixel = TiffBitsPerPixel.Bit48, SkipMetadata = true };
        for (int i = 0; i < source.Frames.Count; i++)
        {
            using Image<Rgba32> reference = source.Frames.CloneFrame(i);
            using MemoryStream single = new();
            reference.Save(single, encoder);
            single.Position = 0;
            using Image<Rgba32> expected = Image.Load<Rgba32>(single);
            ImageSimilarityReport frameReport = ImageComparer.Exact.CompareImagesOrFrames(i, expected.Frames.RootFrame, actual.Frames[i]);
            Assert.True(frameReport.IsEmpty, frameReport.ToString());
        }

        // This frame has no profile. Its samples must remain unchanged even beside profiled frames.
        ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(1, source.Frames[1], actual.Frames[1]);
        Assert.True(report.IsEmpty, report.ToString());
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(firstProfile, source.Metadata.IccProfile);
        Assert.Null(source.Frames[1].Metadata.IccProfile);
    }

    /// <summary>
    /// Checks that incompatible ICC omission and SkipMetadata do not edit the original EXIF profile.
    /// </summary>
    /// <param name="provider">The real RGB image.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, true)]
    public void Encode_IncompatibleProfile_DoesNotWriteStaleExifOrChangeSource(TestImageProvider<Rgba64> provider, bool skipMetadata)
    {
        using Image<Rgba64> source = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39);
        byte[] profileBytes = profile.ToByteArray();

        // TIFF's EXIF reader can retain the raw ICC tag beside the parsed profile.
        ExifByteArray iccTag = new(ExifTagValue.IccProfile, ExifDataType.Undefined) { Value = profileBytes };
        ExifProfile exif = new([iccTag], []);
        source.Frames.RootFrame.Metadata.ExifProfile = exif;
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        byte[] originalExif = exif.ToByteArray();
        using Image<Rgba64> unchanged = source.Clone();

        using MemoryStream encoded = new();
        source.Save(encoded, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit48, SkipMetadata = skipMetadata });
        encoded.Position = 0;
        using Image<Rgba64> actual = Image.Load<Rgba64>(encoded);

        Assert.Null(actual.Frames.RootFrame.Metadata.IccProfile);
        Assert.Equal(originalExif, exif.ToByteArray());
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        ImageComparer.Exact.VerifySimilarity(source, actual);
    }

    /// <summary>
    /// Checks that WhiteIsZero reverses the stored codes and not the decoded appearance.
    /// </summary>
    /// <param name="provider">The real grayscale image.</param>
    /// <param name="depth">The stored sample depth.</param>
    [Theory]
    [WithFile(TestImages.Png.Gray4Bpp, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    [WithFile(TestImages.Png.Gray4Bpp, PixelTypes.Rgba32, TiffBitsPerPixel.Bit16)]
    public void Encode_WhiteIsZero_MatchesBlackIsZero(TestImageProvider<Rgba32> provider, TiffBitsPerPixel depth)
    {
        using Image<Rgba32> source = provider.GetImage();
        using MemoryStream black = new();
        source.Save(black, new TiffEncoder
        {
            BitsPerPixel = depth,
            PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero,
            Compression = TiffCompression.Lzw,
            HorizontalPredictor = TiffPredictor.Horizontal
        });

        black.Position = 0;
        using Image<Rgba64> expected = Image.Load<Rgba64>(black);

        using MemoryStream white = new();
        source.Save(white, new TiffEncoder
        {
            BitsPerPixel = depth,
            PhotometricInterpretation = TiffPhotometricInterpretation.WhiteIsZero,
            Compression = TiffCompression.Lzw,
            HorizontalPredictor = TiffPredictor.Horizontal
        });

        white.Position = 0;
        using Image<Rgba64> actual = Image.Load<Rgba64>(white);

        Assert.Equal(TiffPhotometricInterpretation.WhiteIsZero, actual.Frames.RootFrame.Metadata.GetTiffMetadata().PhotometricInterpretation);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Checks row-local transparent clearing against the existing image operation, without editing the source.
    /// </summary>
    /// <param name="provider">The real image with transparent and partial-alpha pixels.</param>
    /// <param name="depth">The stored sample depth.</param>
    /// <param name="format">The stored sample format.</param>
    /// <param name="interpretation">The explicit sample layout, or null to select it from the depth.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit1, TiffSampleFormat.UnsignedInteger, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit4, TiffSampleFormat.UnsignedInteger, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8, TiffSampleFormat.UnsignedInteger, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit24, TiffSampleFormat.UnsignedInteger, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit64, TiffSampleFormat.UnsignedInteger, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit32, TiffSampleFormat.Float, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit96, TiffSampleFormat.Float, null)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, TiffBitsPerPixel.Bit128, TiffSampleFormat.Float, null)]
    [WithFile(
        TestImages.Webp.Lossy.WithIccpAndAlpha,
        PixelTypes.Rgba32,
        TiffBitsPerPixel.Bit24,
        TiffSampleFormat.UnsignedInteger,
        TiffPhotometricInterpretation.CieLab)]
    [WithFile(
        TestImages.Webp.Lossy.WithIccpAndAlpha,
        PixelTypes.Rgba32,
        TiffBitsPerPixel.Bit48,
        TiffSampleFormat.UnsignedInteger,
        TiffPhotometricInterpretation.CieLab)]
    [WithFile(
        TestImages.Webp.Lossy.WithIccpAndAlpha,
        PixelTypes.Rgba32,
        TiffBitsPerPixel.Bit24,
        TiffSampleFormat.UnsignedInteger,
        TiffPhotometricInterpretation.YCbCr)]
    public void Encode_ClearTransparentPixels_DoesNotChangeSource(
        TestImageProvider<Rgba32> provider,
        TiffBitsPerPixel depth,
        TiffSampleFormat format,
        TiffPhotometricInterpretation? interpretation)
    {
        using Image<Rgba32> source = provider.GetImage();
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;

        using Image<Rgba32> unchanged = source.Clone();

        // Clear a copy through the existing pixel API. Encoding it with Preserve defines the expected Clear output.
        using Image<Rgba32> expected = source.Clone();
        EncodingUtilities.ReplaceTransparentPixels(expected.Frames.RootFrame);

        using MemoryStream reference = new();
        expected.Save(reference, new TiffEncoder { BitsPerPixel = depth, SampleFormat = format, PhotometricInterpretation = interpretation });
        reference.Position = 0;
        using Image<Rgba64> expectedPixels = Image.Load<Rgba64>(reference);

        using MemoryStream encoded = new();
        source.Save(encoded, new TiffEncoder
        {
            BitsPerPixel = depth,
            SampleFormat = format,
            PhotometricInterpretation = interpretation,
            TransparentColorMode = TransparentColorMode.Clear
        });

        encoded.Position = 0;
        using Image<Rgba64> actual = Image.Load<Rgba64>(encoded);

        ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks that an image-wide animation profile applies to every output TIFF directory.
    /// </summary>
    /// <param name="provider">The real APNG animation.</param>
    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32)]
    public void Encode_ImageWideProfile_AppliesToEveryFrame(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> source = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Metadata.IccProfile = profile;
        foreach (ImageFrame<Rgba32> frame in source.Frames)
        {
            frame.Metadata.IccProfile = null;
        }

        using MemoryStream encoded = new();
        source.Save(encoded, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit64, SkipMetadata = true });
        encoded.Position = 0;
        ImageInfo info = Image.Identify(encoded);

        Assert.Equal(source.Frames.Count, info.FrameMetadataCollection.Count);
        foreach (ImageFrameMetadata frame in info.FrameMetadataCollection)
        {
            Assert.Equal(profile.ToByteArray(), frame.IccProfile.ToByteArray());
        }

        Assert.Same(profile, source.Metadata.IccProfile);
        foreach (ImageFrame<Rgba32> frame in source.Frames)
        {
            Assert.Null(frame.Metadata.IccProfile);
        }
    }

    /// <summary>
    /// Checks that TIFF hands the original pixel region and ICC profile to the JPEG encoder exactly once.
    /// </summary>
    /// <typeparam name="TPixel">The source precision.</typeparam>
    /// <param name="provider">The real source image.</param>
    /// <param name="clearTransparentPixels">Whether zero-alpha color is cleared.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32, true)]
    public void Encode_JpegCompression_UsesNestedColorConversion<TPixel>(TestImageProvider<TPixel> provider, bool clearTransparentPixels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Frames.RootFrame.Metadata.IccProfile = profile;
        using Image<TPixel> unchanged = source.Clone();
        using Image<TPixel> reference = source.Clone();
        if (clearTransparentPixels)
        {
            EncodingUtilities.ReplaceTransparentPixels(reference.Frames.RootFrame);
        }

        // A standalone JPEG and a JPEG strip must use the same source precision, profile, and quality.
        using MemoryStream jpeg = new();
        reference.Save(jpeg, new JpegEncoder { ColorType = JpegColorType.Rgb, Quality = 75, SkipMetadata = true });
        jpeg.Position = 0;
        using Image<RgbaVector> expected = Image.Load<RgbaVector>(jpeg);

        TiffEncoder encoder = new()
        {
            BitsPerPixel = TiffBitsPerPixel.Bit24,
            Compression = TiffCompression.Jpeg,
            TransparentColorMode = clearTransparentPixels ? TransparentColorMode.Clear : TransparentColorMode.Preserve,
            SkipMetadata = true
        };

        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        encoded.Position = 0;
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);

        Assert.Equal(TiffCompression.Jpeg, actual.Frames.RootFrame.Metadata.GetTiffMetadata().Compression);
        Assert.Equal(profile.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());
        ImageComparer.Exact.VerifySimilarity(expected, actual);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        source.DebugSave(provider, extension: "tiff", encoder: encoder);
    }

    /// <summary>
    /// Checks grayscale ICC conversion before integer and floating-point packing.
    /// </summary>
    /// <param name="provider">The real grayscale image.</param>
    [Theory]
    [WithFile(TestImages.Png.Gray4Bpp, PixelTypes.Rgba32)]
    public void Encode_IccProfile_TransformsGrayscale(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> source = provider.GetImage();
        using Image<Rgba32> unchanged = source.Clone();
        using Image<RgbaVector> expected = source.CloneAs<RgbaVector>();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.DotGain20);
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        ColorProfileConverter encodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        encodeConverter.Convert(expected);
        expected.Metadata.IccProfile = null;
        expected.Frames.RootFrame.Metadata.IccProfile = null;
        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        (TiffBitsPerPixel Depth, TiffSampleFormat Format)[] layouts =
        [
            (TiffBitsPerPixel.Bit8, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit16, TiffSampleFormat.UnsignedInteger),
            (TiffBitsPerPixel.Bit32, TiffSampleFormat.Float)
        ];

        foreach ((TiffBitsPerPixel depth, TiffSampleFormat format) in layouts)
        {
            TiffEncoder encoder = new()
            {
                BitsPerPixel = depth,
                SampleFormat = format,
                PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero,
                Compression = TiffCompression.Deflate,
                SkipMetadata = true
            };

            using MemoryStream reference = new();
            if (format == TiffSampleFormat.Float)
            {
                // RGB float storage keeps the profile's repeated gray value unchanged. The gray ICC
                // reader uses its first component, so this reference adds no second luminance calculation.
                expected.Save(reference, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit96, SampleFormat = format });
            }
            else
            {
                expected.Save(reference, encoder);
            }

            reference.Position = 0;
            using Image<RgbaVector> expectedPixels = Image.Load<RgbaVector>(reference);
            decodeConverter.Convert(expectedPixels);

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);
            encoded.Position = 0;
            using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);

            Assert.Equal(profile.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());
            Assert.Equal(depth, actual.Frames.RootFrame.Metadata.GetTiffMetadata().BitsPerPixel);
            ImageComparer.Exact.VerifySimilarity(expectedPixels, actual);
            ImageComparer.Exact.VerifySimilarity(unchanged, source);
        }
    }

    /// <summary>
    /// Checks the retained profile and appearance of a real bilevel image through the reference decoder.
    /// </summary>
    /// <param name="provider">The real bilevel TIFF.</param>
    [Theory]
    [WithFile(TestImages.Tiff.Fax4Compressed, PixelTypes.Rgba32)]
    public void Encode_IccProfile_BilevelRetainsProfileAndAppearance(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> source = provider.GetImage();
        using Image<Rgba32> unchanged = source.Clone();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.DotGain20);
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        TiffEncoder encoder = new() { BitsPerPixel = TiffBitsPerPixel.Bit1, Compression = TiffCompression.Deflate };
        string output = source.VerifyEncoder(provider, "tiff", null, encoder, ImageComparer.Exact, referenceDecoder: ReferenceDecoder);
        ImageInfo info = Image.Identify(output);

        Assert.Equal(profile.ToByteArray(), info.FrameMetadataCollection[0].IccProfile.ToByteArray());
        Assert.Equal(TiffBitsPerPixel.Bit1, info.FrameMetadataCollection[0].GetTiffMetadata().BitsPerPixel);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks float grayscale output with the reference decoder at every supported SIMD width and in scalar execution.
    /// </summary>
    /// <param name="provider">The real grayscale image.</param>
    [Theory]
    [WithFile(TestImages.Png.Gray4Bpp, PixelTypes.RgbaVector)]
    public void Encode_FloatGrayscale_AcrossHardwareConfigurations(TestImageProvider<RgbaVector> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunFloatGrayscaleEncode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Checks grayscale float encoding for BlackIsZero and WhiteIsZero in the selected hardware configuration.
    /// </summary>
    /// <param name="providerDump">The serialized real-image provider.</param>
    /// <param name="intrinsics">The hardware configuration used for output naming.</param>
    private static void RunFloatGrayscaleEncode(string providerDump, string intrinsics)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        using Image<RgbaVector> source = provider.GetImage();

        // Sixty-five real pixels exercise full 512-, 256- and 128-bit groups plus one scalar pixel. Four rows exercise row-buffer reuse.
        source.Mutate(x => x.Crop(new Rectangle(0, 0, 65, 4)));
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;

        TiffPhotometricInterpretation[] interpretations = [TiffPhotometricInterpretation.BlackIsZero, TiffPhotometricInterpretation.WhiteIsZero];
        foreach (TiffPhotometricInterpretation interpretation in interpretations)
        {
            TiffEncoder encoder = new()
            {
                BitsPerPixel = TiffBitsPerPixel.Bit32,
                SampleFormat = TiffSampleFormat.Float,
                PhotometricInterpretation = interpretation,
                Compression = TiffCompression.None
            };

            // The reference decoder exports unprofiled TIFF samples at 16-bit precision. Allow one ushort code per RGB component.
            // A zero image threshold applies that bound to every pixel.
            source.VerifyEncoder(
                provider,
                "tiff",
                $"{interpretation}_{intrinsics}",
                encoder,
                ImageComparer.Tolerant(0F, 3),
                referenceDecoder: ReferenceDecoder);
        }
    }

    /// <summary>
    /// Checks that float ICC output restores associated alpha and writes the matching ExtraSamples tag.
    /// </summary>
    /// <param name="provider">The real image with zero, partial, and opaque alpha.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.RgbaVectorP)]
    public void Encode_IccProfile_PreservesAssociatedFloatAlpha(TestImageProvider<RgbaVectorP> provider)
    {
        using Image<RgbaVectorP> source = provider.GetImage();
        using Image<RgbaVectorP> unchanged = source.Clone();
        using Image<RgbaVector> converted = source.CloneAs<RgbaVector>();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        source.Frames.RootFrame.Metadata.IccProfile = profile;

        ColorProfileConverter encodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        encodeConverter.Convert(converted);
        converted.Metadata.IccProfile = null;
        converted.Frames.RootFrame.Metadata.IccProfile = null;

        // Associate the converted profile values through the pixel API before storing them.
        // Reading those samples back reproduces the rounding from TIFF's associated float storage.
        using Image<RgbaVectorP> stored = converted.CloneAs<RgbaVectorP>();
        TiffEncoder encoder = new() { BitsPerPixel = TiffBitsPerPixel.Bit128, SampleFormat = TiffSampleFormat.Float };
        using MemoryStream reference = new();
        stored.Save(reference, encoder);
        reference.Position = 0;
        using Image<RgbaVectorP> expected = Image.Load<RgbaVectorP>(reference);

        ColorProfileConverter decodeConverter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = provider.Configuration.MemoryAllocator
        });

        decodeConverter.Convert(expected);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        encoded.Position = 0;
        using Image<RgbaVectorP> actual = Image.Load<RgbaVectorP>(encoded);

        Assert.Equal(TiffExtraSampleType.AssociatedAlphaData, actual.Frames.RootFrame.Metadata.GetTiffMetadata().ExtraSampleType);
        Assert.Equal(profile.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());
        ImageComparer.Exact.VerifySimilarity(expected, actual);
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks native grayscale and CMYK JPEG output with and without a compatible frame profile.
    /// </summary>
    /// <param name="provider">The real source image.</param>
    /// <param name="photometric">The TIFF sample interpretation.</param>
    /// <param name="jpegColorType">The matching JPEG component layout.</param>
    /// <param name="depth">The TIFF sample depth.</param>
    /// <param name="profileFile">The profile fixture, or null to retain the source TIFF profile.</param>
    [Theory]
    [WithFile(
        TestImages.Png.Gray4Bpp,
        PixelTypes.RgbaVector,
        TiffPhotometricInterpretation.BlackIsZero,
        JpegColorType.Luminance,
        TiffBitsPerPixel.Bit8,
        TestIccProfiles.DotGain20)]
    [WithFile(
        TestImages.Tiff.Issues2454_B,
        PixelTypes.RgbaVector,
        TiffPhotometricInterpretation.Separated,
        JpegColorType.Cmyk,
        TiffBitsPerPixel.Bit32,
        null)]
    public void Encode_JpegCompression_UsesNativeGrayAndCmyk(
        TestImageProvider<RgbaVector> provider,
        TiffPhotometricInterpretation photometric,
        JpegColorType jpegColorType,
        TiffBitsPerPixel depth,
        string profileFile)
    {
        using Image<RgbaVector> source = provider.GetImage();
        using Image<RgbaVector> unchanged = source.Clone();
        IccProfile originalImageProfile = source.Metadata.IccProfile;
        IccProfile profile = profileFile is null
            ? source.Frames.RootFrame.Metadata.IccProfile
            : TestIccProfiles.GetProfile(profileFile);

        Assert.NotNull(profile);

        TiffEncoder encoder = new()
        {
            BitsPerPixel = depth,
            PhotometricInterpretation = photometric,
            Compression = TiffCompression.Jpeg,
            SkipMetadata = true
        };

        foreach (bool includeProfile in new[] { false, true })
        {
            IccProfile selectedProfile = includeProfile ? profile : null;
            source.Frames.RootFrame.Metadata.IccProfile = selectedProfile;
            using Image<RgbaVector> reference = source.Clone();
            reference.Metadata.IccProfile = selectedProfile;

            using MemoryStream jpeg = new();
            reference.Save(jpeg, new JpegEncoder { ColorType = jpegColorType, Quality = 75, SkipMetadata = true });
            jpeg.Position = 0;
            using Image<RgbaVector> expected = Image.Load<RgbaVector>(jpeg);

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);
            encoded.Position = 0;
            using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);
            TiffFrameMetadata metadata = actual.Frames.RootFrame.Metadata.GetTiffMetadata();
            ExifProfile exif = actual.Frames.RootFrame.Metadata.ExifProfile;

            Assert.Equal(TiffCompression.Jpeg, metadata.Compression);
            Assert.Equal(photometric, metadata.PhotometricInterpretation);
            Assert.Equal(depth, metadata.BitsPerPixel);
            Assert.Equal(selectedProfile?.ToByteArray(), actual.Frames.RootFrame.Metadata.IccProfile?.ToByteArray());
            Assert.Null(metadata.ExtraSampleType);

            if (photometric == TiffPhotometricInterpretation.Separated)
            {
                // All four samples carry CMYK color. The directory must describe four inks without an alpha sample.
                Assert.Equal(new TiffBitsPerSample(8, 8, 8, 8), metadata.BitsPerSample);
                Assert.Equal((ushort)TiffInkSet.Cmyk, exif.GetValue(ExifTag.InkSet).Value);
                Assert.Equal((ushort)4, exif.GetValue(ExifTag.NumberOfInks).Value);
            }

            ImageComparer.Exact.VerifySimilarity(expected, actual);
            ImageComparer.Exact.VerifySimilarity(unchanged, source);
            Assert.Same(selectedProfile, source.Frames.RootFrame.Metadata.IccProfile);
            Assert.Same(originalImageProfile, source.Metadata.IccProfile);
            source.DebugSave(provider, testOutputDetails: $"{jpegColorType}_{includeProfile}", extension: "tiff", encoder: encoder);
        }
    }
}
