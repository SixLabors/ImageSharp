// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Gif;

[Trait("Format", "Gif")]
public class GifMetadataTests
{
    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new()
        {
            { TestImages.Gif.Rings, (int)ImageMetadata.DefaultHorizontalResolution, (int)ImageMetadata.DefaultVerticalResolution, PixelResolutionUnit.PixelsPerInch },
            { TestImages.Gif.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
            { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
        };

    public static readonly TheoryData<string, uint> RepeatFiles =
        new()
        {
            { TestImages.Gif.Cheers, 0 },
            { TestImages.Gif.Receipt, 1 },
            { TestImages.Gif.Rings, 1 }
        };

    [Fact]
    public void CloneIsDeep()
    {
        var meta = new GifMetadata
        {
            RepeatCount = 1,
            ColorTableMode = GifColorTableMode.Global,
            GlobalColorTableLength = 2,
            Comments = new List<string> { "Foo" }
        };

        var clone = (GifMetadata)meta.DeepClone();

        clone.RepeatCount = 2;
        clone.ColorTableMode = GifColorTableMode.Local;
        clone.GlobalColorTableLength = 1;

        Assert.False(meta.RepeatCount.Equals(clone.RepeatCount));
        Assert.False(meta.ColorTableMode.Equals(clone.ColorTableMode));
        Assert.False(meta.GlobalColorTableLength.Equals(clone.GlobalColorTableLength));
        Assert.False(meta.Comments.Equals(clone.Comments));
        Assert.True(meta.Comments.SequenceEqual(clone.Comments));
    }

    [Fact]
    public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
    {
        var testFile = TestFile.Create(TestImages.Gif.Rings);

        using Image<Rgba32> image = testFile.CreateRgba32Image(new GifDecoder());
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(1, metadata.Comments.Count);
        Assert.Equal("ImageSharp", metadata.Comments[0]);
    }

    [Fact]
    public void Decode_IgnoreMetadataIsTrue_CommentsAreIgnored()
    {
        DecoderOptions options = new()
        {
            SkipMetadata = true
        };

        var testFile = TestFile.Create(TestImages.Gif.Rings);

        using Image<Rgba32> image = testFile.CreateRgba32Image(new GifDecoder(), options);
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(0, metadata.Comments.Count);
    }

    [Fact]
    public void Decode_CanDecodeLargeTextComment()
    {
        var testFile = TestFile.Create(TestImages.Gif.LargeComment);

        using Image<Rgba32> image = testFile.CreateRgba32Image(new GifDecoder());
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(2, metadata.Comments.Count);
        Assert.Equal(new string('c', 349), metadata.Comments[0]);
        Assert.Equal("ImageSharp", metadata.Comments[1]);
    }

    [Fact]
    public void Encode_PreservesTextData()
    {
        var decoder = new GifDecoder();
        var testFile = TestFile.Create(TestImages.Gif.LargeComment);

        using Image<Rgba32> input = testFile.CreateRgba32Image(decoder);
        using var memoryStream = new MemoryStream();
        input.Save(memoryStream, new GifEncoder());
        memoryStream.Position = 0;

        using Image<Rgba32> image = decoder.Decode<Rgba32>(DecoderOptions.Default, memoryStream);
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(2, metadata.Comments.Count);
        Assert.Equal(new string('c', 349), metadata.Comments[0]);
        Assert.Equal("ImageSharp", metadata.Comments[1]);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        IImageInfo image = decoder.Identify(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public async Task Identify_VerifyRatioAsync(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        IImageInfo image = await decoder.IdentifyAsync(DecoderOptions.Default, stream, default);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        using Image<Rgba32> image = decoder.Decode<Rgba32>(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public async Task Decode_VerifyRatioAsync(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        using Image<Rgba32> image = await decoder.DecodeAsync<Rgba32>(DecoderOptions.Default, stream, default);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RepeatFiles))]
    public void Identify_VerifyRepeatCount(string imagePath, uint repeatCount)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        IImageInfo image = decoder.Identify(DecoderOptions.Default, stream);
        GifMetadata meta = image.Metadata.GetGifMetadata();
        Assert.Equal(repeatCount, meta.RepeatCount);
    }

    [Theory]
    [MemberData(nameof(RepeatFiles))]
    public void Decode_VerifyRepeatCount(string imagePath, uint repeatCount)
    {
        var testFile = TestFile.Create(imagePath);
        using var stream = new MemoryStream(testFile.Bytes, false);
        var decoder = new GifDecoder();
        using Image<Rgba32> image = decoder.Decode<Rgba32>(DecoderOptions.Default, stream);
        GifMetadata meta = image.Metadata.GetGifMetadata();
        Assert.Equal(repeatCount, meta.RepeatCount);
    }
}
