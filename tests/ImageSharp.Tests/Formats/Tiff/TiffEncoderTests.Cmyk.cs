// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

public partial class TiffEncoderTests
{
    /// <summary>
    /// Checks unprofiled CMYK packing and prediction against the reference decoder at every SIMD width and in scalar execution.
    /// </summary>
    /// <param name="provider">The real sixteen-bit RGB image.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64)]
    public void Encode_Cmyk_UnprofiledMatchesReferenceAcrossHardware(TestImageProvider<Rgba64> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunUnprofiledCmykEncode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Runs real RGB pixels through both CMYK depths and the supported lossless compression and prediction combinations.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    /// <param name="intrinsics">The hardware mode used in output names.</param>
    private static void RunUnprofiledCmykEncode(string providerDump, string intrinsics)
    {
        TestImageProvider<Rgba64> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba64>>(providerDump);
        using Image<Rgba64> source = provider.GetImage();

        // Width 66 leaves two pixels after full SIMD pixel groups, and one predicted pixel after full sample groups.
        // Ninety-nine rows require several strips at both depths, including a shorter final strip.
        source.Mutate(x => x.Crop(new Rectangle(320, 384, 66, 99)));
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;
        using Image<Rgba64> unchanged = source.Clone();

        foreach (TiffBitsPerPixel depth in new[] { TiffBitsPerPixel.Bit32, TiffBitsPerPixel.Bit64 })
        {
            foreach (TiffCompression compression in new[] { TiffCompression.None, TiffCompression.PackBits, TiffCompression.Lzw, TiffCompression.Deflate })
            {
                foreach (TiffPredictor predictor in compression is TiffCompression.Lzw or TiffCompression.Deflate
                    ? new[] { TiffPredictor.None, TiffPredictor.Horizontal }
                    : new[] { TiffPredictor.None })
                {
                    TiffEncoder encoder = new()
                    {
                        BitsPerPixel = depth,
                        PhotometricInterpretation = TiffPhotometricInterpretation.Separated,
                        Compression = compression,
                        HorizontalPredictor = predictor
                    };

                    // CMYK quantizes each ink once. The RGB result can differ by one output code per channel.
                    int pixelThreshold = depth == TiffBitsPerPixel.Bit32 ? 3 * (ushort.MaxValue / byte.MaxValue) : 3;
                    string output = source.VerifyEncoder(
                        provider,
                        "tiff",
                        $"{depth}_{compression}_{predictor}_{intrinsics}",
                        encoder,
                        ImageComparer.Tolerant(0F, pixelThreshold),
                        referenceDecoder: ReferenceDecoder);

                    ImageInfo info = Image.Identify(output);
                    TiffFrameMetadata metadata = info.FrameMetadataCollection[0].GetTiffMetadata();
                    ushort bits = depth == TiffBitsPerPixel.Bit32 ? (ushort)8 : (ushort)16;
                    Assert.Equal(TiffPhotometricInterpretation.Separated, metadata.PhotometricInterpretation);
                    Assert.Equal(new TiffBitsPerSample(bits, bits, bits, bits), metadata.BitsPerSample);
                    Assert.Equal(compression, metadata.Compression);
                    Assert.Equal(predictor, metadata.Predictor);
                    Assert.Null(metadata.ExtraSampleType);
                    Assert.Null(info.FrameMetadataCollection[0].IccProfile);
                }
            }
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks that native CMYK output retains its profile and agrees with the reference decoder after ICC conversion.
    /// </summary>
    /// <param name="provider">The real profiled CMYK image.</param>
    [Theory]
    [WithFile(TestImages.Tiff.Issue3198, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Tiff.Icc.PerceptualCmyk, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Tiff.Issues2454_B, PixelTypes.RgbaVector)]
    public void Encode_Cmyk_ProfiledMatchesReference(TestImageProvider<RgbaVector> provider)
    {
        using Image<RgbaVector> source = provider.GetImage();

        // Keep the reporter's small samples intact. A central photograph region supplies varied colors and SIMD tails.
        int width = Math.Min(257, source.Width);
        int height = Math.Min(129, source.Height);
        source.Mutate(x => x.Crop(new Rectangle((source.Width - width) / 2, (source.Height - height) / 2, width, height)));
        IccProfile profile = Assert.IsType<IccProfile>(source.Frames.RootFrame.Metadata.IccProfile);
        byte[] profileBytes = profile.ToByteArray();
        using Image<RgbaVector> unchanged = source.Clone();

        // Input_RgbFloat contains the exact cropped RGB pixels before CMYK conversion. All Output_Cmyk files use this input.
        // Its RGB layout omits the incompatible CMYK profile without changing the source samples or metadata.
        source.DebugSave(provider, testOutputDetails: "Input_RgbFloat", extension: "tiff", encoder: new TiffEncoder
        {
            BitsPerPixel = TiffBitsPerPixel.Bit96,
            SampleFormat = TiffSampleFormat.Float,
            PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
            Compression = TiffCompression.None,
            SkipMetadata = true
        });

        foreach (TiffBitsPerPixel depth in new[] { TiffBitsPerPixel.Bit32, TiffBitsPerPixel.Bit64 })
        {
            foreach (TiffCompression compression in new[] { TiffCompression.None, TiffCompression.PackBits, TiffCompression.Lzw, TiffCompression.Deflate })
            {
                TiffEncoder encoder = new()
                {
                    BitsPerPixel = depth,
                    PhotometricInterpretation = TiffPhotometricInterpretation.Separated,
                    Compression = compression,
                    HorizontalPredictor = TiffPredictor.Horizontal,
                    SkipMetadata = true
                };

                using MemoryStream encoded = new();
                source.Save(encoded, encoder);
                encoded.Position = 0;
                using Image<RgbaVector> expected = ReferenceDecoder.Decode<RgbaVector>(DecoderOptions.Default, encoded);
                encoded.Position = 0;
                using Image<RgbaVector> actual = Image.Load<RgbaVector>(encoded);
                TiffFrameMetadata metadata = actual.Frames.RootFrame.Metadata.GetTiffMetadata();
                ExifProfile exif = actual.Frames.RootFrame.Metadata.ExifProfile;

                // Use the existing ICC reference bound: at most three eight-bit RGB codes in total for each pixel.
                ImageComparer.Tolerant(0F, 3 * (ushort.MaxValue / byte.MaxValue)).VerifySimilarity(expected, actual);
                Assert.Equal(depth, metadata.BitsPerPixel);
                Assert.Equal(TiffPhotometricInterpretation.Separated, metadata.PhotometricInterpretation);
                Assert.Null(metadata.ExtraSampleType);
                Assert.Equal((ushort)TiffInkSet.Cmyk, exif.GetValue(ExifTag.InkSet).Value);
                Assert.Equal((ushort)4, exif.GetValue(ExifTag.NumberOfInks).Value);
                Assert.Equal(profileBytes, actual.Frames.RootFrame.Metadata.IccProfile.ToByteArray());

                if (compression == TiffCompression.None)
                {
                    // Inspect the stored inks too. Two decoders could agree even if the encoder skipped its return transform.
                    AssertCmykSamples(source, encoded.ToArray(), exif, depth, profile);
                }

                source.DebugSave(provider, testOutputDetails: $"Output_Cmyk_{depth}_{compression}", extension: "tiff", encoder: encoder);
            }
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks uncompressed ink samples against scalar ICC conversion, using parsed strip tags to locate the samples.
    /// </summary>
    /// <param name="source">The original straight working RGB samples.</param>
    /// <param name="file">The complete uncompressed TIFF.</param>
    /// <param name="exif">The decoded directory entries.</param>
    /// <param name="depth">The total CMYK pixel depth.</param>
    /// <param name="profile">The retained CMYK profile.</param>
    private static void AssertCmykSamples(Image<RgbaVector> source, byte[] file, ExifProfile exif, TiffBitsPerPixel depth, IccProfile profile)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = CompactSrgbV4Profile.Profile,
            TargetIccProfile = profile,
            MemoryAllocator = source.Configuration.MemoryAllocator
        });

        Number[] offsets = exif.GetValue(ExifTag.StripOffsets).Value;
        int rowsPerStrip = (int)exif.GetValue(ExifTag.RowsPerStrip).Value;
        bool byteSamples = depth == TiffBitsPerPixel.Bit32;
        int maximumCode = byteSamples ? byte.MaxValue : ushort.MaxValue;
        int bytesPerRow = source.Width * 4 * (byteSamples ? 1 : sizeof(ushort));

        // Apply the recorded formula bound separately to each ink, plus half a stored code for final sample rounding.
        // This does not allow an intermediate eight-bit conversion when the file declares sixteen-bit samples.
        ApproximateColorProfileComparer comparer = new(5E-4F + (.5F / maximumCode));

        for (int y = 0; y < source.Height; y++)
        {
            int offset = (int)offsets[y / rowsPerStrip] + ((y % rowsPerStrip) * bytesPerRow);
            ReadOnlySpan<byte> bytes = file.AsSpan(offset, bytesPerRow);

            // The encoder declares and writes the host byte order. Read its ushort words without changing their bit depth.
            ReadOnlySpan<ushort> words = MemoryMarshal.Cast<byte, ushort>(bytes);
            ReadOnlySpan<RgbaVector> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);

            for (int x = 0; x < row.Length; x++)
            {
                RgbaVector pixel = row[x];
                Rgb rgb = new(pixel.R, pixel.G, pixel.B);
                Cmyk expected = converter.Convert<Rgb, Cmyk>(in rgb);
                int component = x * 4;

                // Four consecutive components are C, M, Y and K. No pixel alpha conversion is involved.
                Cmyk actual = byteSamples
                    ? new(
                        bytes[component] / (float)maximumCode,
                        bytes[component + 1] / (float)maximumCode,
                        bytes[component + 2] / (float)maximumCode,
                        bytes[component + 3] / (float)maximumCode)
                    : new(
                        words[component] / (float)maximumCode,
                        words[component + 1] / (float)maximumCode,
                        words[component + 2] / (float)maximumCode,
                        words[component + 3] / (float)maximumCode);

                Assert.Equal(expected, actual, comparer);
            }
        }
    }

    /// <summary>
    /// Checks transparent RGB clearing before alpha becomes the fourth CMYK ink sample.
    /// </summary>
    /// <param name="provider">The real image with hidden RGB under transparent pixels.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithIccpAndAlpha, PixelTypes.RgbaVector)]
    public void Encode_Cmyk_ClearsTransparentRgbWithoutChangingSource(TestImageProvider<RgbaVector> provider)
    {
        using Image<RgbaVector> source = provider.GetImage();
        source.Frames.RootFrame.Metadata.IccProfile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39);
        using Image<RgbaVector> unchanged = source.Clone();
        using Image<RgbaVector> reference = source.Clone();
        EncodingUtilities.ReplaceTransparentPixels(reference.Frames.RootFrame);

        // The fixture must contain hidden color so this comparison exercises clearing, rather than encoding identical inputs.
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(source, reference));

        foreach (TiffBitsPerPixel depth in new[] { TiffBitsPerPixel.Bit32, TiffBitsPerPixel.Bit64 })
        {
            using MemoryStream expected = new();
            reference.Save(expected, new TiffEncoder
            {
                BitsPerPixel = depth,
                PhotometricInterpretation = TiffPhotometricInterpretation.Separated,
                Compression = TiffCompression.Lzw,
                TransparentColorMode = TransparentColorMode.Preserve
            });

            using MemoryStream actual = new();
            source.Save(actual, new TiffEncoder
            {
                BitsPerPixel = depth,
                PhotometricInterpretation = TiffPhotometricInterpretation.Separated,
                Compression = TiffCompression.Lzw,
                TransparentColorMode = TransparentColorMode.Clear
            });

            // Row-local clearing must match clearing the source copy through the existing image operation.
            Assert.Equal(expected.ToArray(), actual.ToArray());
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }
}
