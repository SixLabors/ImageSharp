// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

public partial class TiffEncoderTests
{
    /// <summary>
    /// Checks TIFF YCbCr components and lossless compression with and without a retained RGB profile.
    /// </summary>
    /// <param name="provider">The real RGB source image.</param>
    /// <param name="retainProfile">Whether to retain and apply the source profile on output.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, false)]
    [WithFile(TestImages.Tiff.Icc.PerceptualRgb8, PixelTypes.Rgba64, true)]
    public void Encode_YCbCr_PreservesSamplesAcrossHardware(TestImageProvider<Rgba64> provider, bool retainProfile)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunYCbCrEncode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            retainProfile.ToString());

    /// <summary>
    /// Checks full-resolution YCbCr output through the normal writer with all supported lossless compression modes.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    /// <param name="retainProfileValue">Whether the test retains the real input profile.</param>
    private static void RunYCbCrEncode(string providerDump, string retainProfileValue)
    {
        TestImageProvider<Rgba64> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba64>>(providerDump);
        using Image<Rgba64> source = provider.GetImage();
        int width = Math.Min(66, source.Width);
        int height = Math.Min(99, source.Height);

        // Preserve real image colors while exercising complete SIMD groups, a two-pixel tail and several strips.
        source.Mutate(x => x.Crop(new Rectangle((source.Width - width) / 2, (source.Height - height) / 2, width, height)));

        if (!bool.Parse(retainProfileValue))
        {
            source.Metadata.IccProfile = null;
            source.Frames.RootFrame.Metadata.IccProfile = null;
        }

        IccProfile originalImageProfile = source.Metadata.IccProfile;
        IccProfile originalFrameProfile = source.Frames.RootFrame.Metadata.IccProfile;
        IccProfile profile = originalFrameProfile ?? originalImageProfile;
        byte[] profileBytes = profile?.ToByteArray();

        if (bool.Parse(retainProfileValue))
        {
            Assert.NotNull(profile);
        }

        using Image<Rgba64> unchanged = source.Clone();

        // Save the exact working RGB codes for the separate libtiff/LittleCMS component comparison.
        // Remove the profile from the existing comparison copy so saving it does not apply a return transform.
        unchanged.Metadata.IccProfile = null;
        unchanged.Frames.RootFrame.Metadata.IccProfile = null;
        unchanged.DebugSave(provider, testOutputDetails: "YCbCrInput", extension: "tiff", encoder: new TiffEncoder
        {
            BitsPerPixel = TiffBitsPerPixel.Bit48,
            PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
            Compression = TiffCompression.None,
            SkipMetadata = true
        });

        using MemoryStream uncompressed = new();

        // Check stored components before decoding, then require every lossless mode to match this decoded result.
        source.Save(uncompressed, new TiffEncoder
        {
            BitsPerPixel = TiffBitsPerPixel.Bit24,
            PhotometricInterpretation = TiffPhotometricInterpretation.YCbCr,
            Compression = TiffCompression.None
        });

        uncompressed.Position = 0;
        using Image<Rgba64> expected = Image.Load<Rgba64>(uncompressed);

        foreach (TiffCompression compression in new[] { TiffCompression.None, TiffCompression.PackBits, TiffCompression.Lzw, TiffCompression.Deflate })
        {
            TiffEncoder encoder = new()
            {
                BitsPerPixel = TiffBitsPerPixel.Bit24,
                PhotometricInterpretation = TiffPhotometricInterpretation.YCbCr,
                Compression = compression,
                HorizontalPredictor = TiffPredictor.Horizontal,
                SkipMetadata = true
            };

            using MemoryStream encoded = new();
            source.Save(encoded, encoder);
            source.DebugSave(provider, testOutputDetails: compression, extension: "tiff", encoder: encoder);

            encoded.Position = 0;
            using Image<Rgba64> actual = Image.Load<Rgba64>(encoded);
            TiffFrameMetadata metadata = actual.Frames.RootFrame.Metadata.GetTiffMetadata();
            ExifProfile exif = actual.Frames.RootFrame.Metadata.ExifProfile;

            Assert.Equal(TiffPhotometricInterpretation.YCbCr, metadata.PhotometricInterpretation);
            Assert.Equal(new TiffBitsPerSample(8, 8, 8), metadata.BitsPerSample);

            Assert.Equal(compression, metadata.Compression);
            Assert.Equal(compression is TiffCompression.Lzw or TiffCompression.Deflate ? TiffPredictor.Horizontal : TiffPredictor.None, metadata.Predictor);

            // These tags must describe the newly written samples, even when source metadata is skipped.
            Assert.Equal(new ushort[] { 1, 1 }, exif.GetValue(ExifTag.YCbCrSubsampling).Value);
            Assert.Equal(TiffYCbCrOperator.DefaultCoefficients.ToArray(), exif.GetValue(ExifTag.YCbCrCoefficients).Value);
            Assert.Equal(TiffYCbCrOperator.DefaultReferenceLevels.ToArray(), exif.GetValue(ExifTag.ReferenceBlackWhite).Value);

            Assert.Equal(profileBytes, actual.Frames.RootFrame.Metadata.IccProfile?.ToByteArray());

            if (compression == TiffCompression.None)
            {
                AssertYCbCrSamples(source, encoded.ToArray(), exif, profile);
            }

            // Sample values are checked above before decoding. Lossless compression must retain their decoded result exactly.
            // Magick's generic matrix uses chroma center 127.5; this TIFF declares 128, so it is not the reference for this layout.
            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);

        Assert.Same(originalImageProfile, source.Metadata.IccProfile);
        Assert.Same(originalFrameProfile, source.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks stored YCbCr bytes before decoding, including the RGB ICC return transform when present.
    /// </summary>
    /// <param name="source">The working RGB pixels.</param>
    /// <param name="file">The uncompressed TIFF bytes.</param>
    /// <param name="exif">The directory containing strip offsets.</param>
    /// <param name="profile">The retained RGB profile, or null.</param>
    private static void AssertYCbCrSamples(Image<Rgba64> source, byte[] file, ExifProfile exif, IccProfile profile)
    {
        Number[] offsets = exif.GetValue(ExifTag.StripOffsets).Value;
        int rowsPerStrip = (int)exif.GetValue(ExifTag.RowsPerStrip).Value;
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = profile is null ? null : CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile
        });

        ColorConversionOptions options = new() { YCbCrTransform = new TiffYCbCrOperator().ProfileTransform };

        // Half a byte code covers final packing. The established formula bound covers scalar versus SIMD ICC evaluation.
        ApproximateColorProfileComparer comparer = new(5E-4F + (.5F / byte.MaxValue));

        for (int y = 0; y < source.Height; y++)
        {
            int offset = (int)offsets[y / rowsPerStrip] + ((y % rowsPerStrip) * source.Width * 3);
            ReadOnlySpan<byte> row = file.AsSpan(offset, source.Width * 3);

            for (int x = 0; x < source.Width; x++)
            {
                Rgba64 pixel = source[x, y];
                Rgb rgb = new(pixel.R / (float)ushort.MaxValue, pixel.G / (float)ushort.MaxValue, pixel.B / (float)ushort.MaxValue);

                if (profile is not null)
                {
                    rgb = converter.Convert<Rgb, Rgb>(in rgb);
                }

                YCbCr expected = YCbCr.FromProfileConnectingSpace(options, in rgb);
                YCbCr actual = new(row[x * 3] / 255F, row[(x * 3) + 1] / 255F, row[(x * 3) + 2] / 255F);

                Assert.Equal(expected, actual, comparer);
            }
        }
    }
}
