// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
[ValidateDisposedMemoryAllocations]
public class HeifEncoderTests
{
    [Fact]
    public void OptionsHaveExpectedDefaults()
    {
        HeifEncoder encoder = new();

        Assert.Equal(HeifCompressionMethod.LegacyJpeg, encoder.CompressionMethod);
        Assert.Null(encoder.Quality);
        Assert.Null(encoder.AlphaQuality);
        Assert.Equal(5, encoder.Effort);
        Assert.False(encoder.Lossless);
        Assert.Null(encoder.BitDepth);
        Assert.Null(encoder.ChromaSubsampling);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void QualityOutsideRangeThrows(int quality)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { Quality = quality });

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AlphaQualityOutsideRangeThrows(int quality)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { AlphaQuality = quality });

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void EffortOutsideRangeThrows(int effort)
        => Assert.Throws<ArgumentException>(() => new HeifEncoder { Effort = effort });

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 10)]
    public void OptionRangeBoundariesAreAccepted(int quality, int alphaQuality, int effort)
    {
        HeifEncoder encoder = new()
        {
            Quality = quality,
            AlphaQuality = alphaQuality,
            Effort = effort
        };

        Assert.Equal(quality, encoder.Quality);
        Assert.Equal(alphaQuality, encoder.AlphaQuality);
        Assert.Equal(effort, encoder.Effort);
    }

    [Fact]
    public void LegacyJpegRejectsZeroQuality()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Quality = 0 };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
    }

    [Fact]
    public void LegacyJpegRejectsLosslessEncoding()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Lossless = true };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
    }

    [Theory]
    [InlineData(HeifBitDepth.Bit10)]
    [InlineData(HeifBitDepth.Bit12)]
    public void LegacyJpegRejectsHighBitDepth(HeifBitDepth bitDepth)
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { BitDepth = bitDepth };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
    }

    [Theory]
    [WithFile(TestImages.Heif.Sample640x427, PixelTypes.Rgba32, HeifCompressionMethod.LegacyJpeg)]
    public static void Encode<TPixel>(TestImageProvider<TPixel> provider, HeifCompressionMethod compressionMethod)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(new MagickReferenceDecoder(HeifFormat.Instance));
        using MemoryStream stream = new();
        HeifEncoder encoder = new();
        image.Save(stream, encoder);
        stream.Position = 0;

        ImageInfo imageInfo = Image.Identify(stream);
        Assert.Equal(image.Size, imageInfo.Size);

        stream.Position = 0;
        using Image<TPixel> encodedImage = Image.Load<TPixel>(stream);
        HeifMetadata heifMetadata = encodedImage.Metadata.GetHeifMetadata();

        ImageComparer.Exact.CompareImages(image, encodedImage);
        Assert.Equal(compressionMethod, heifMetadata.CompressionMethod);
    }
}
