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
        Assert.Null(encoder.RepeatCount);
        Assert.True(encoder.AnimateRootFrame);
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
    public void LegacyJpegAcceptsZeroQuality()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Quality = 0 };

        image.Save(stream, encoder);

        Assert.NotEqual(0, stream.Length);
        stream.Position = 0;
        using Image<Rgba32> decoded = Image.Load<Rgba32>(stream);
        Assert.Equal(image.Size, decoded.Size);
    }

    [Fact]
    public void LegacyJpegRejectsLosslessEncoding()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { Lossless = true };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
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
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Av1RejectsEncodingBeforeWritingOutput()
    {
        using Image<Rgba32> image = new(1, 1);
        using MemoryStream stream = new();
        HeifEncoder encoder = new() { CompressionMethod = HeifCompressionMethod.Av1 };

        Assert.Throws<NotSupportedException>(() => image.Save(stream, encoder));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void LegacyJpegEncodingDoesNotMutateSourceHeifMetadata()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30, 255);
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        metadata.CompressionMethod = HeifCompressionMethod.Av1;
        using MemoryStream stream = new();
        HeifEncoder encoder = new();

        image.Save(stream, encoder);

        Assert.Same(metadata, image.Metadata.GetHeifMetadata());
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
    }

    [Theory]
    [WithFile(TestImages.Heif.IrvineAvif, PixelTypes.Rgba32, HeifCompressionMethod.LegacyJpeg)]
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
