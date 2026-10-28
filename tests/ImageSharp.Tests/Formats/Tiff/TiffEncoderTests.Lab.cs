// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

public partial class TiffEncoderTests
{
    /// <summary>
    /// Checks Lab sample packing, compression and prediction through the encoder at every SIMD width.
    /// </summary>
    /// <param name="provider">The real sixteen-bit RGB image.</param>
    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64)]
    public void Encode_Lab_PreservesSamplesAcrossHardware(TestImageProvider<Rgba64> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunLabEncode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Checks both Lab depths with complete vector groups, a scalar tail and multiple strips.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    /// <param name="intrinsics">The hardware mode used in output names.</param>
    private static void RunLabEncode(string providerDump, string intrinsics)
    {
        TestImageProvider<Rgba64> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba64>>(providerDump);
        using Image<Rgba64> source = provider.GetImage();

        // Two remaining pixels exercise packing tails. Ninety-nine rows require several TIFF strips.
        source.Mutate(x => x.Crop(new Rectangle(320, 384, 66, 99)));
        source.Metadata.IccProfile = null;
        source.Frames.RootFrame.Metadata.IccProfile = null;

        using Image<Rgba64> unchanged = source.Clone();

        // The independent native comparison reads these exact RGB codes before any Lab conversion.
        source.DebugSave(provider, testOutputDetails: "LabInput", extension: "tiff", encoder: new TiffEncoder
        {
            BitsPerPixel = TiffBitsPerPixel.Bit48,
            PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
            Compression = TiffCompression.None,
            SkipMetadata = true
        });

        foreach (TiffBitsPerPixel depth in new[] { TiffBitsPerPixel.Bit24, TiffBitsPerPixel.Bit48 })
        {
            // Check stored components before decoding, then require every lossless mode to match this decoded result.
            using MemoryStream uncompressed = new();
            source.Save(uncompressed, new TiffEncoder
            {
                BitsPerPixel = depth,
                PhotometricInterpretation = TiffPhotometricInterpretation.CieLab,
                Compression = TiffCompression.None
            });

            uncompressed.Position = 0;
            using Image<Rgba64> expected = Image.Load<Rgba64>(uncompressed);

            foreach (TiffCompression compression in new[] { TiffCompression.None, TiffCompression.PackBits, TiffCompression.Lzw, TiffCompression.Deflate })
            {
                TiffEncoder encoder = new()
                {
                    BitsPerPixel = depth,
                    PhotometricInterpretation = TiffPhotometricInterpretation.CieLab,
                    Compression = compression,
                    HorizontalPredictor = TiffPredictor.Horizontal
                };

                using MemoryStream encoded = new();
                source.Save(encoded, encoder);
                source.DebugSave(provider, testOutputDetails: $"{depth}_{compression}_{intrinsics}", extension: "tiff", encoder: encoder);

                encoded.Position = 0;
                using Image<Rgba64> actual = Image.Load<Rgba64>(encoded);
                TiffFrameMetadata metadata = actual.Frames.RootFrame.Metadata.GetTiffMetadata();
                ushort bits = depth == TiffBitsPerPixel.Bit24 ? (ushort)8 : (ushort)16;

                Assert.Equal(TiffPhotometricInterpretation.CieLab, metadata.PhotometricInterpretation);
                Assert.Equal(new TiffBitsPerSample(bits, bits, bits), metadata.BitsPerSample);
                Assert.Null(metadata.ExtraSampleType);

                Assert.Equal(compression, metadata.Compression);
                Assert.Equal(compression is TiffCompression.Lzw or TiffCompression.Deflate ? TiffPredictor.Horizontal : TiffPredictor.None, metadata.Predictor);

                Assert.Null(actual.Frames.RootFrame.Metadata.IccProfile);

                if (compression == TiffCompression.None)
                {
                    AssertLabSamples(source, encoded.ToArray(), actual.Frames.RootFrame.Metadata.ExifProfile, depth);
                }

                // Lossless compression and horizontal prediction must preserve the independently checked uncompressed samples exactly.
                ImageComparer.Exact.VerifySimilarity(expected, actual);
            }
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
    }

    /// <summary>
    /// Checks uncompressed lightness and signed chroma against scalar RGB-to-Lab conversion before any TIFF decoding.
    /// </summary>
    /// <param name="source">The original working RGB pixels.</param>
    /// <param name="file">The encoded TIFF bytes.</param>
    /// <param name="exif">The parsed directory containing strip offsets.</param>
    /// <param name="depth">The encoded sample depth.</param>
    private static void AssertLabSamples(Image<Rgba64> source, byte[] file, ExifProfile exif, TiffBitsPerPixel depth)
    {
        Number[] offsets = exif.GetValue(ExifTag.StripOffsets).Value;
        int rowsPerStrip = (int)exif.GetValue(ExifTag.RowsPerStrip).Value;
        bool sixteenBit = depth == TiffBitsPerPixel.Bit48;
        int bytesPerPixel = sixteenBit ? 6 : 3;
        ColorProfileConverter converter = new();

        // Use the established 5E-4 formula bound in the color model's comparison units, plus half a stored chroma code.
        // Lab's comparison maps 255 chroma units to one. Sixteen-bit storage has 256 codes per chroma unit.
        float quantization = .5F / (byte.MaxValue * (sixteenBit ? 256F : 1F));
        ApproximateColorProfileComparer comparer = new(5E-4F + quantization, scaled: true);

        for (int y = 0; y < source.Height; y++)
        {
            int offset = (int)offsets[y / rowsPerStrip] + ((y % rowsPerStrip) * source.Width * bytesPerPixel);
            ReadOnlySpan<byte> row = file.AsSpan(offset, source.Width * bytesPerPixel);
            ReadOnlySpan<ushort> words = MemoryMarshal.Cast<byte, ushort>(row);

            for (int x = 0; x < source.Width; x++)
            {
                Rgba64 pixel = source[x, y];
                Rgb rgb = new(pixel.R / (float)ushort.MaxValue, pixel.G / (float)ushort.MaxValue, pixel.B / (float)ushort.MaxValue);
                CieLab expected = converter.Convert<Rgb, CieLab>(in rgb);

                // The TIFF header and sample words use host byte order. Cast chroma back to its signed storage type.
                CieLab actual = sixteenBit
                    ? new CieLab(
                        words[x * 3] * (100F / ushort.MaxValue),
                        unchecked((short)words[(x * 3) + 1]) / 256F,
                        unchecked((short)words[(x * 3) + 2]) / 256F)
                    : new CieLab(row[x * 3] * (100F / byte.MaxValue), unchecked((sbyte)row[(x * 3) + 1]), unchecked((sbyte)row[(x * 3) + 2]));

                Assert.Equal(expected, actual, comparer);
            }
        }
    }
}
