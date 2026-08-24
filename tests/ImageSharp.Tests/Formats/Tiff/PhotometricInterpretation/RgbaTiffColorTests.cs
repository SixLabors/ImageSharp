// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

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
            RgbaTiffColor<Rgba32> color = new(TiffExtraSampleType.UnassociatedAlphaData, new TiffBitsPerSample(4, 4, 4, 8));
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
            RgbaTiffColor<Rgba32> color = new(TiffExtraSampleType.AssociatedAlphaData, new TiffBitsPerSample(8, 8, 8, 8));
            color.Decode(input, pixels, 0, 0, 1, 1);
        });
    }

    [Fact]
    public void DecodeUnassociatedAlphaAssociatesDestinationStorage()
    {
        byte[] input = [128, 64, 32, 128];
        using Image<Rgba32P> image = new(1, 1);

        RgbaTiffColor<Rgba32P> color = new(TiffExtraSampleType.UnassociatedAlphaData, new TiffBitsPerSample(8, 8, 8, 8));
        color.Decode(input, image.Frames.RootFrame.PixelBuffer, 0, 0, 1, 1);

        // The decoder receives straight TIFF channels, while Rgba32P stores RGB multiplied by its quantized alpha byte.
        Assert.Equal(new Rgba32P(64, 32, 16, 128), image[0, 0]);
    }
}
