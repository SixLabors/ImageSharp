// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
public class HeifMetadataTests
{
    [Fact]
    public void DefaultsMatchLegacyEightBitHeif()
    {
        HeifMetadata metadata = new();

        Assert.Equal(HeifCompressionMethod.LegacyJpeg, metadata.CompressionMethod);
        Assert.Equal(HeifBitDepth.Bit8, metadata.BitDepth);
        Assert.False(metadata.IsMonochrome);
        Assert.False(metadata.HasAlpha);
        Assert.Equal(1, metadata.RepeatCount);
        Assert.True(metadata.AnimateRootFrame);
    }

    [Fact]
    public void DeepCloneCopiesImageDescription()
    {
        HeifMetadata metadata = new()
        {
            CompressionMethod = HeifCompressionMethod.Av1,
            BitDepth = HeifBitDepth.Bit12,
            IsMonochrome = true,
            HasAlpha = true,
            RepeatCount = 3,
            AnimateRootFrame = false
        };

        HeifMetadata clone = metadata.DeepClone();

        Assert.Equal(metadata.CompressionMethod, clone.CompressionMethod);
        Assert.Equal(metadata.BitDepth, clone.BitDepth);
        Assert.Equal(metadata.IsMonochrome, clone.IsMonochrome);
        Assert.Equal(metadata.HasAlpha, clone.HasAlpha);
        Assert.Equal(metadata.RepeatCount, clone.RepeatCount);
        Assert.Equal(metadata.AnimateRootFrame, clone.AnimateRootFrame);
    }

    [Fact]
    public void SequenceStateRoundTripsFormatConnectingMetadata()
    {
        FormatConnectingMetadata connectingMetadata = new()
        {
            AnimateRootFrame = false,
            PixelTypeInfo = new PixelTypeInfo(24),
            RepeatCount = 7
        };

        HeifMetadata metadata = HeifMetadata.FromFormatConnectingMetadata(connectingMetadata);
        FormatConnectingMetadata result = metadata.ToFormatConnectingMetadata();

        Assert.False(result.AnimateRootFrame);
        Assert.Equal(7, result.RepeatCount);
    }

    [Theory]
    [InlineData(1, HeifBitDepth.Bit8)]
    [InlineData(8, HeifBitDepth.Bit8)]
    [InlineData(9, HeifBitDepth.Bit10)]
    [InlineData(10, HeifBitDepth.Bit10)]
    [InlineData(11, HeifBitDepth.Bit12)]
    [InlineData(16, HeifBitDepth.Bit12)]
    public void FromFormatConnectingMetadataSelectsSupportedBitDepth(int componentPrecision, HeifBitDepth expected)
    {
        FormatConnectingMetadata connectingMetadata = new()
        {
            PixelTypeInfo = new PixelTypeInfo(componentPrecision)
            {
                ComponentInfo = PixelComponentInfo.Create(1, componentPrecision, componentPrecision)
            }
        };

        HeifMetadata metadata = HeifMetadata.FromFormatConnectingMetadata(connectingMetadata);

        Assert.Equal(expected, metadata.BitDepth);
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit8, false, false, 24, 3)]
    [InlineData(HeifBitDepth.Bit10, false, true, 40, 4)]
    [InlineData(HeifBitDepth.Bit12, true, false, 12, 1)]
    [InlineData(HeifBitDepth.Bit12, true, true, 24, 2)]
    public void GetPixelTypeInfoUsesComponentBitDepth(
        HeifBitDepth bitDepth,
        bool isMonochrome,
        bool hasAlpha,
        int expectedBitsPerPixel,
        int expectedComponentCount)
    {
        HeifMetadata metadata = new()
        {
            BitDepth = bitDepth,
            IsMonochrome = isMonochrome,
            HasAlpha = hasAlpha
        };

        PixelTypeInfo pixelTypeInfo = metadata.GetPixelTypeInfo();
        PixelComponentInfo componentInfo = pixelTypeInfo.ComponentInfo.Value;

        Assert.Equal(expectedBitsPerPixel, pixelTypeInfo.BitsPerPixel);
        Assert.Equal(expectedComponentCount, componentInfo.ComponentCount);
        Assert.Equal((int)bitDepth, componentInfo.GetMaximumComponentPrecision());
    }

    [Theory]
    [InlineData(8, HeifBitDepth.Bit8)]
    [InlineData(10, HeifBitDepth.Bit10)]
    [InlineData(12, HeifBitDepth.Bit12)]
    public void HevcConfigurationAcceptsExposedBitDepths(int componentBitDepth, HeifBitDepth expected)
    {
        HevcCodecConfiguration configuration = new(CreateHevcCodecConfiguration(componentBitDepth));

        Assert.Equal(expected, configuration.BitDepth);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void HevcConfigurationRejectsUnexposedBitDepths(int componentBitDepth)
    {
        byte[] configuration = CreateHevcCodecConfiguration(componentBitDepth);

        Assert.Throws<InvalidImageContentException>(() => new HevcCodecConfiguration(configuration));
    }

    /// <summary>
    /// Creates the fixed HEVC decoder-configuration record needed to exercise component bit-depth validation.
    /// </summary>
    /// <param name="componentBitDepth">The luma and chroma sample precision to encode in the record.</param>
    /// <returns>The complete configuration record without parameter-set arrays.</returns>
    private static byte[] CreateHevcCodecConfiguration(int componentBitDepth)
    {
        byte[] data = new byte[23];
        data[0] = 1;
        data[13] = 0xF0;
        data[15] = 0xFC;
        data[16] = 0xFD;
        data[17] = (byte)(0xF8 | (componentBitDepth - 8));
        data[18] = (byte)(0xF8 | (componentBitDepth - 8));
        data[21] = 3;

        // An empty array list is sufficient here because bit-depth validation belongs to the fixed record and runs
        // before parameter-set matching. Parameter-set conformance is covered separately by the container tests.
        return data;
    }
}
