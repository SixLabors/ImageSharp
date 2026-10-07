// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class RgbaTiffColorTests : PhotometricInterpretationTestBase
{
    [Fact]
    public void DecodeUsesAlphaChannelBitDepth()
    {
        // The single pixel packs 4-bit RGB followed by 8-bit alpha. Using the blue-channel width for alpha would consume
        // only the first alpha nibble and leave the decoded opacity at 8 instead of 128.
        byte[] input = [0xF0, 0x88, 0x00];
        Rgba32[][] expected = [[new Rgba32(255, 0, 136, 128)]];

        AssertDecode(expected, pixels =>
        {
            RgbaTiffColor<Rgba32> color = new(Configuration.Default, TiffExtraSampleType.UnassociatedAlphaData, new TiffBitsPerSample(4, 4, 4, 8));
            color.Decode(input, pixels, 0, 0, 1, 1);
        });
    }

    [Fact]
    public void DecodeAssociatedAlphaRestoresLogicalColor()
    {
        byte[] input = [64, 32, 16, 128];
        Rgba32[][] expected = [[new Rgba32(128, 64, 32, 128)]];

        AssertDecode(expected, pixels =>
        {
            RgbaTiffColor<Rgba32> color = new(Configuration.Default, TiffExtraSampleType.AssociatedAlphaData, new TiffBitsPerSample(8, 8, 8, 8));
            color.Decode(input, pixels, 0, 0, 1, 1);
        });
    }

    [Fact]
    public void DecodeUnassociatedAlphaAssociatesDestinationStorage()
    {
        byte[] input = [128, 64, 32, 128];
        using Image<Rgba32P> image = new(1, 1);

        RgbaTiffColor<Rgba32P> color = new(Configuration.Default, TiffExtraSampleType.UnassociatedAlphaData, new TiffBitsPerSample(8, 8, 8, 8));
        color.Decode(input, image.Frames.RootFrame.PixelBuffer, 0, 0, 1, 1);

        // The decoder receives straight TIFF channels, while Rgba32P stores RGB multiplied by its quantized alpha byte.
        Assert.Equal(new Rgba32P(64, 32, 16, 128), image[0, 0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecodeRgba16_BigEndianMatchesLittleEndianForTargetPixelTypes(bool associated) => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertRgba16ByteOrders, associated, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks known 16-bit colors after endian conversion and destination alpha packing.
    /// </summary>
    /// <param name="associatedText">Whether stored RGB is premultiplied.</param>
    private static void AssertRgba16ByteOrders(string associatedText)
    {
        bool associated = FeatureTestRunner.Deserialize<bool>(associatedText);
        TiffExtraSampleType extraSamplesType = associated ? TiffExtraSampleType.AssociatedAlphaData : TiffExtraSampleType.UnassociatedAlphaData;
        const int width = 65;

        // Every code is an eight-bit value times 257, giving exact expected target bytes.
        // The three-sample period crosses complete 32-pixel ushort groups and the final scalar tail.
        ReadOnlySpan<ushort> samplePixels = [16448, 8224, 4112, 32896, 0, 0, 0, 0, 51400, 25700, 12850, 65535];
        ushort[] samples = new ushort[width * 4];

        for (int x = 0; x < width; x++)
        {
            samplePixels.Slice((x % 3) * 4, 4).CopyTo(samples.AsSpan(x * 4, 4));
        }

        byte[] littleEndian = new byte[samples.Length * 2];
        byte[] bigEndian = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(littleEndian.AsSpan(i * 2), samples[i]);
            BinaryPrimitives.WriteUInt16BigEndian(bigEndian.AsSpan(i * 2), samples[i]);
        }

        using Image<Rgba32> littleRgba32 = new(width, 1);
        using Image<Rgba32> bigRgba32 = new(width, 1);
        using Image<Rgba32P> littleRgba32P = new(width, 1);
        using Image<Rgba32P> bigRgba32P = new(width, 1);

        new TiffRgba64LittleEndianDecoder<Rgba32>(Configuration.Default, extraSamplesType == TiffExtraSampleType.AssociatedAlphaData).Decode(littleEndian, littleRgba32.Frames.RootFrame.PixelBuffer, 0, 0, width, 1);
        new TiffRgba64BigEndianDecoder<Rgba32>(Configuration.Default, extraSamplesType == TiffExtraSampleType.AssociatedAlphaData).Decode(bigEndian, bigRgba32.Frames.RootFrame.PixelBuffer, 0, 0, width, 1);
        new TiffRgba64LittleEndianDecoder<Rgba32P>(Configuration.Default, extraSamplesType == TiffExtraSampleType.AssociatedAlphaData).Decode(littleEndian, littleRgba32P.Frames.RootFrame.PixelBuffer, 0, 0, width, 1);
        new TiffRgba64BigEndianDecoder<Rgba32P>(Configuration.Default, extraSamplesType == TiffExtraSampleType.AssociatedAlphaData).Decode(bigEndian, bigRgba32P.Frames.RootFrame.PixelBuffer, 0, 0, width, 1);

        for (int x = 0; x < width; x++)
        {
            Rgba32 expected = (x % 3) switch { 0 => associated ? new Rgba32(128, 64, 32, 128) : new Rgba32(64, 32, 16, 128), 1 => new Rgba32(0, 0, 0, 0), _ => new Rgba32(200, 100, 50, 255) };
            Rgba32P expectedAssociated = (x % 3) switch { 0 => associated ? new Rgba32P(64, 32, 16, 128) : new Rgba32P(32, 16, 8, 128), 1 => new Rgba32P(0, 0, 0, 0), _ => new Rgba32P(200, 100, 50, 255) };
            Assert.Equal(expected, littleRgba32[x, 0]);
            Assert.Equal(expected, bigRgba32[x, 0]);
            Assert.Equal(expectedAssociated, littleRgba32P[x, 0]);
            Assert.Equal(expectedAssociated, bigRgba32P[x, 0]);
        }
    }
}
