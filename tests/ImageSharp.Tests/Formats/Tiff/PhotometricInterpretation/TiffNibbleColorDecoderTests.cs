// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Checks libtiff-produced four-bit RGB and RGBA rows across full SIMD groups and final pixels.
/// </summary>
[Trait("Format", "Tiff")]
public class TiffNibbleColorDecoderTests
{
    // The 17-sample pattern shifts colors between four-pixel SIMD groups, revealing channel swaps.
    private static readonly (byte R, byte G, byte B)[] RgbSamples =
    [
        (4, 8, 12), (0, 15, 0), (15, 0, 3), (1, 5, 9), (2, 6, 10),
        (3, 7, 11), (5, 9, 13), (6, 10, 14), (7, 11, 15), (8, 12, 0),
        (9, 13, 1), (10, 14, 2), (11, 15, 4), (12, 0, 5), (13, 1, 6),
        (14, 2, 7), (15, 3, 8)
    ];

    // Zero alpha retains nonzero stored RGB to test the associated-alpha clearing rule.
    private static readonly (byte R, byte G, byte B, byte A)[] RgbaSamples =
    [
        (3, 5, 7, 10), (4, 5, 6, 0), (1, 2, 9, 10), (2, 4, 8, 10),
        (0, 7, 3, 10), (5, 1, 6, 10), (7, 3, 2, 10), (8, 0, 4, 10),
        (9, 6, 1, 10), (4, 9, 5, 10), (6, 8, 0, 10), (10, 2, 3, 10),
        (1, 10, 7, 10), (3, 0, 10, 10), (2, 9, 4, 10), (5, 7, 8, 10),
        (9, 1, 6, 10)
    ];

    /// <summary>
    /// Compares RGB and straight-alpha TIFF pixels with an independent TIFF decoder.
    /// </summary>
    /// <param name="path">The source TIFF fixture.</param>
    [Theory]
    [InlineData(Rgb44465X3)]
    [InlineData(Rgba4444Straight65X3)]
    public void Decode_MatchesMagickReference(string path) => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertMagickReference, path, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares every decoded pixel with the independent TIFF decoder.
    /// </summary>
    /// <param name="path">The source TIFF fixture.</param>
    private static void AssertMagickReference(string path)
    {
        using Image<Rgba32> image = TestFile.Create(path).CreateRgba32Image(TiffDecoder.Instance);
        using Image<Rgba32> reference = TestFile.Create(path).CreateRgba32Image(MagickReferenceDecoder.Tiff);

        ImageComparer.Exact.VerifySimilarity(reference, image);
    }

    /// <summary>
    /// Compares associated-alpha pixels with an independent TIFF decoder in premultiplied storage.
    /// </summary>
    [Fact]
    public void DecodeAssociated_MatchesMagickReference() => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertAssociatedReference, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares the associated image while preserving the separate hidden-RGB numeric check.
    /// </summary>
    private static void AssertAssociatedReference()
    {
        // Both decoders produce premultiplied RGB; the numeric test also verifies that zero alpha clears RGB.
        using MemoryStream imageStream = new(TestFile.Create(Rgba4444Associated65X3).Bytes);
        using MemoryStream referenceStream = new(TestFile.Create(Rgba4444Associated65X3).Bytes);
        using Image<Rgba32P> image = TiffDecoder.Instance.Decode<Rgba32P>(DecoderOptions.Default, imageStream);
        using Image<Rgba32P> reference = MagickReferenceDecoder.Tiff.Decode<Rgba32P>(DecoderOptions.Default, referenceStream);

        ImageComparer.Exact.VerifySimilarity(reference, image);
    }

    /// <summary>
    /// Checks stored RGB nibble values across byte-padded rows and SIMD tails.
    /// </summary>
    [Fact]
    public void DecodeRgb444_PreservesKnownColorsAcrossRowsAndTail() => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertRgbFixture, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks each decoded RGB pixel against the source sample table.
    /// </summary>
    private static void AssertRgbFixture()
    {
        using Image<RgbaVector> image = Image.Load<RgbaVector>(TestFile.Create(Rgb44465X3).Bytes);

        // Sixty-five pixels fill four sixteen-pixel blocks and leave one scalar pixel.
        // Two-row strips put the third row in a new strip.
        Assert.Equal(new Size(65, 3), image.Size);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // The generator starts each row one sample later, exposing stale colors at row boundaries.
                (byte r, byte g, byte b) = RgbSamples[(x + y) % RgbSamples.Length];

                // Unsigned TIFF four-bit codes span 0–15; dividing by 15 makes code 15 full intensity.
                RgbaVector expected = RgbaVector.FromUnassociatedScaledVector4(new Vector4(r / 15F, g / 15F, b / 15F, 1F));
                Assert.Equal(expected, image[x, y]);
            }
        }
    }

    /// <summary>
    /// Checks straight and associated RGBA nibble samples, including zero alpha.
    /// </summary>
    /// <param name="path">The source TIFF fixture.</param>
    /// <param name="associated">Whether TIFF declares premultiplied RGB.</param>
    [Theory]
    [InlineData(Rgba4444Straight65X3, false)]
    [InlineData(Rgba4444Associated65X3, true)]
    public void DecodeRgba4444_PreservesKnownAlphaAcrossRowsAndTail(string path, bool associated) => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertRgbaFixture, path, associated.ToString(), HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks each decoded RGBA pixel against the source sample table.
    /// </summary>
    /// <param name="path">The source TIFF fixture.</param>
    /// <param name="associatedText">The serialized alpha interpretation.</param>
    private static void AssertRgbaFixture(string path, string associatedText)
    {
        bool associated = bool.Parse(associatedText);
        using Image<RgbaVector> image = Image.Load<RgbaVector>(TestFile.Create(path).Bytes);

        // Sixty-five pixels fill four sixteen-pixel blocks and leave one scalar pixel.
        // Two-row strips put the third row in a new strip.
        Assert.Equal(new Size(65, 3), image.Size);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // The generator starts each row one sample later, exposing stale colors at row boundaries.
                (byte r, byte g, byte b, byte a) = RgbaSamples[(x + y) % RgbaSamples.Length];

                // Unsigned TIFF four-bit codes span 0–15; dividing by 15 makes code 15 full intensity.
                Vector4 codes = new(r / 15F, g / 15F, b / 15F, a / 15F);

                // The TIFF alpha tag says whether stored RGB is premultiplied; RgbaVector stores straight RGB.
                RgbaVector expected = associated ? RgbaVector.FromAssociatedScaledVector4(codes) : RgbaVector.FromUnassociatedScaledVector4(codes);
                Assert.Equal(expected, image[x, y]);
            }
        }
    }
}
