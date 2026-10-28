// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Compares complete high-depth TIFF decodes with eight-bit RGB references exported from their libtiff scanlines.
/// </summary>
[Trait("Format", "Tiff")]
public class TiffHighDepthReferenceTests
{
    /// <summary>
    /// Checks all 24 sample bits before float conversion, including unaligned input and each SIMD width.
    /// </summary>
    [Fact]
    public void Read24BitSamples_PreservesIntegerCodes() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertIntegerCodes,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks byte-order boundaries and values that an eight-bit image reference cannot distinguish.
    /// </summary>
    private static void AssertIntegerCodes()
    {
        // Each three-byte group is one big-endian integer. The cases cross byte boundaries and include the highest 24-bit codes.
        ReadOnlySpan<byte> encoded =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0xFF, 0x00, 0x01, 0x00,
            0x00, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x12, 0x34, 0x56, 0x7F, 0xFF, 0xFF,
            0x80, 0x00, 0x00, 0x98, 0x76, 0x54, 0xAB, 0xCD, 0xEF, 0xFE, 0xDC, 0xBA,
            0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0x01, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF
        ];

        uint[] expected =
        [
            0x000000, 0x000001, 0x0000FF, 0x000100, 0x00FFFF, 0x010000, 0x123456, 0x7FFFFF,
            0x800000, 0x987654, 0xABCDEF, 0xFEDCBA, 0xFFFF00, 0xFFFF01, 0xFFFFFE, 0xFFFFFF
        ];

        foreach (bool bigEndian in new[] { false, true })
        {
            foreach (int count in new[] { 4, 8, 16 })
            {
                // Skip one byte so the source is unaligned. Its span contains exactly the requested codes.
                byte[] storage = new byte[(count * 3) + 1];
                Span<byte> source = storage.AsSpan(1);
                encoded[..source.Length].CopyTo(source);
                if (!bigEndian)
                {
                    // Reversing the first and third byte gives the same known codes in little-endian order.
                    for (int i = 0; i < source.Length; i += 3)
                    {
                        (source[i], source[i + 2]) = (source[i + 2], source[i]);
                    }
                }

                uint[] actual = new uint[count];
                if (count == 4)
                {
                    TiffUInt24Samples.ReadFour(source, bigEndian).StoreUnsafe(ref actual[0]);
                }
                else if (count == 8)
                {
                    TiffUInt24Samples.Read(source, bigEndian, out Vector256<uint> values);
                    values.StoreUnsafe(ref actual[0]);
                }
                else
                {
                    TiffUInt24Samples.Read(source, bigEndian, out Vector512<uint> values);
                    values.StoreUnsafe(ref actual[0]);
                }

                Assert.Equal(expected[..count], actual);
            }
        }
    }

    /// <summary>
    /// Checks packed and planar RGB/RGBA with 24 or 32 bits per channel in both byte orders.
    /// </summary>
    /// <param name="sourcePath">The existing high-depth TIFF sample.</param>
    /// <param name="referencePath">The independent libtiff RGB8 reference.</param>
    [Theory]
    [InlineData(FlowerRgb242424Contiguous, FlowerRgb24ContiguousReference)]
    [InlineData(FlowerRgb242424ContiguousLittleEndian, FlowerRgb24ContiguousLittleEndianReference)]
    [InlineData(FlowerRgb242424Planar, FlowerRgb24PlanarReference)]
    [InlineData(FlowerRgb242424PlanarLittleEndian, FlowerRgb24PlanarLittleEndianReference)]
    [InlineData(FlowerRgb323232Contiguous, FlowerRgb32ContiguousReference)]
    [InlineData(FlowerRgb323232ContiguousLittleEndian, FlowerRgb32ContiguousLittleEndianReference)]
    [InlineData(FlowerRgb323232Planar, FlowerRgb32PlanarReference)]
    [InlineData(FlowerRgb323232PlanarLittleEndian, FlowerRgb32PlanarLittleEndianReference)]
    [InlineData(Rgba24BitUnassociatedAlphaBigEndian, Rgba24BigEndianReference)]
    [InlineData(Rgba24BitUnassociatedAlphaLittleEndian, Rgba24LittleEndianReference)]
    [InlineData(Rgba24BitPlanarUnassociatedAlphaBigEndian, Rgba24PlanarBigEndianReference)]
    [InlineData(Rgba24BitPlanarUnassociatedAlphaLittleEndian, Rgba24PlanarLittleEndianReference)]
    [InlineData(Rgba32BitUnassociatedAlphaBigEndian, Rgba32BigEndianReference)]
    [InlineData(Rgba32BitUnassociatedAlphaLittleEndian, Rgba32LittleEndianReference)]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaBigEndian, Rgba32PlanarBigEndianReference)]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaLittleEndian, Rgba32PlanarLittleEndianReference)]
    public void Decode_MatchesLibtiffReference(string sourcePath, string referencePath) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertReference,
        sourcePath,
        referencePath,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares all decoded pixels with libtiff's independently rounded output.
    /// </summary>
    /// <param name="sourcePath">The existing high-depth TIFF sample.</param>
    /// <param name="referencePath">The independent libtiff RGB8 reference.</param>
    private static void AssertReference(string sourcePath, string referencePath)
    {
        using Image<Rgba32> image = TestFile.Create(sourcePath).CreateRgba32Image(TiffDecoder.Instance);
        using Image<Rgba32> reference = TestFile.Create(referencePath).CreateRgba32Image(MagickReferenceDecoder.Tiff);

        ImageComparer.Exact.VerifySimilarity(reference, image);
    }
}
