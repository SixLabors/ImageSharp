// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
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

}
