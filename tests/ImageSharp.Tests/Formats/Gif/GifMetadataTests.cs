// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Webp;
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

    /// <summary>
    /// Identifying an ICC-bearing GIF assembles the complete profile when optional metadata is skipped or retained.
    /// </summary>
    /// <param name="skipMetadata">Whether optional metadata is skipped.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Identify_ExifToolIcc_AssemblesCompleteProfile(bool skipMetadata)
    {
        TestFile testFile = TestFile.Create(TestImages.Gif.ExifToolIcc);
        DecoderOptions options = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve, SkipMetadata = skipMetadata };

        // ICC remains available for color handling even when optional metadata is skipped.
        ImageInfo info = Image.Identify(options, testFile.Bytes);
        GifMetadata metadata = info.Metadata.GetGifMetadata();

        Assert.NotNull(metadata.GlobalColorTable);
        ReadOnlySpan<Color> palette = metadata.GlobalColorTable.Value.Span;

        Assert.Equal(new Size(8, 8), info.Size);
        Assert.Single(info.FrameMetadataCollection);
        Assert.Equal(256, palette.Length);
        Assert.Equal(palette[0], metadata.BackgroundColor);

        // The source profile spans two GIF application sub-blocks; its checksum detects truncated assembly.
        byte[] profileHash = SHA256.HashData(info.Metadata.IccProfile.ToByteArray());

        Assert.Equal("A8D0D753BD6129357CC2647435CE675E8637A679EB526FA180FBA460874CE1D3", Convert.ToHexString(profileHash));
    }

    [Fact]
    public void FormatConnectingMetadata_PreservesBackgroundBetweenGifAndWebp()
    {
        WebpMetadata webp = new() { BackgroundColor = Color.CornflowerBlue };
        GifMetadata gif = GifMetadata.FromFormatConnectingMetadata(webp.ToFormatConnectingMetadata());
        WebpMetadata roundTrip = WebpMetadata.FromFormatConnectingMetadata(gif.ToFormatConnectingMetadata());

        Assert.Equal(webp.BackgroundColor, gif.BackgroundColor);
        Assert.Equal(webp.BackgroundColor, roundTrip.BackgroundColor);
    }

    [Fact]
    public void CloneIsDeep()
    {
        GifMetadata meta = new()
        {
            RepeatCount = 1,
            ColorTableMode = FrameColorTableMode.Global,
            GlobalColorTable = new[] { Color.Black, Color.White },
            Comments = ["Foo"]
        };

        GifMetadata clone = (GifMetadata)meta.DeepClone();

        clone.RepeatCount = 2;
        clone.ColorTableMode = FrameColorTableMode.Local;
        clone.GlobalColorTable = new[] { Color.Black };

        Assert.False(meta.RepeatCount.Equals(clone.RepeatCount));
        Assert.False(meta.ColorTableMode.Equals(clone.ColorTableMode));
        Assert.False(meta.GlobalColorTable.Value.Length == clone.GlobalColorTable.Value.Length);
        Assert.Equal(1, clone.GlobalColorTable.Value.Length);
        Assert.False(meta.Comments.Equals(clone.Comments));
        Assert.True(meta.Comments.SequenceEqual(clone.Comments));
    }

    [Fact]
    public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
    {
        TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

        using Image<Rgba32> image = testFile.CreateRgba32Image(GifDecoder.Instance);
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(1, metadata.Comments.Count);
        Assert.Equal("ImageSharp", metadata.Comments[0]);
    }

    [Fact]
    public void Decode_IgnoreMetadataIsTrue_CommentsAreIgnored()
    {
        DecoderOptions options = new(Configuration.Default)
        {
            SkipMetadata = true
        };

        TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

        using Image<Rgba32> image = testFile.CreateRgba32Image(GifDecoder.Instance, options);
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(0, metadata.Comments.Count);
    }

    [Fact]
    public void Decode_CanDecodeLargeTextComment()
    {
        TestFile testFile = TestFile.Create(TestImages.Gif.LargeComment);

        using Image<Rgba32> image = testFile.CreateRgba32Image(GifDecoder.Instance);
        GifMetadata metadata = image.Metadata.GetGifMetadata();
        Assert.Equal(2, metadata.Comments.Count);
        Assert.Equal(new string('c', 349), metadata.Comments[0]);
        Assert.Equal("ImageSharp", metadata.Comments[1]);
    }

    [Fact]
    public void Encode_PreservesTextData()
    {
        GifDecoder decoder = GifDecoder.Instance;
        TestFile testFile = TestFile.Create(TestImages.Gif.LargeComment);

        using Image<Rgba32> input = testFile.CreateRgba32Image(decoder);
        using MemoryStream memoryStream = new();
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
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = GifDecoder.Instance.Identify(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public async Task Identify_VerifyRatioAsync(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        await using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = await GifDecoder.Instance.IdentifyAsync(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image<Rgba32> image = GifDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public async Task Decode_VerifyRatioAsync(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        await using MemoryStream stream = new(testFile.Bytes, false);
        using Image<Rgba32> image = await GifDecoder.Instance.DecodeAsync<Rgba32>(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(RepeatFiles))]
    public void Identify_VerifyRepeatCount(string imagePath, uint repeatCount)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = GifDecoder.Instance.Identify(DecoderOptions.Default, stream);
        GifMetadata meta = image.Metadata.GetGifMetadata();
        Assert.Equal(repeatCount, meta.RepeatCount);
    }

    [Theory]
    [MemberData(nameof(RepeatFiles))]
    public void Decode_VerifyRepeatCount(string imagePath, uint repeatCount)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image<Rgba32> image = GifDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream);
        GifMetadata meta = image.Metadata.GetGifMetadata();
        Assert.Equal(repeatCount, meta.RepeatCount);
    }

    [Theory]
    [InlineData(TestImages.Gif.Cheers, 93, FrameColorTableMode.Global, 256, 4, FrameDisposalMode.DoNotDispose)]
    public void Identify_Frames(
        string imagePath,
        int framesCount,
        FrameColorTableMode colorTableMode,
        int globalColorTableLength,
        int frameDelay,
        FrameDisposalMode disposalMethod)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        GifMetadata gifMetadata = imageInfo.Metadata.GetGifMetadata();
        Assert.NotNull(gifMetadata);

        Assert.Equal(framesCount, imageInfo.FrameMetadataCollection.Count);
        GifFrameMetadata gifFrameMetadata = imageInfo.FrameMetadataCollection[imageInfo.FrameMetadataCollection.Count - 1].GetGifMetadata();

        Assert.Equal(colorTableMode, gifFrameMetadata.ColorTableMode);

        if (colorTableMode == FrameColorTableMode.Global)
        {
            Assert.Equal(globalColorTableLength, gifMetadata.GlobalColorTable.Value.Length);
        }

        Assert.Equal(frameDelay, gifFrameMetadata.FrameDelay);
        Assert.Equal(disposalMethod, gifFrameMetadata.DisposalMode);
    }

    [Theory]
    [InlineData(TestImages.Gif.Issues.BadMaxLzwBits, 8)]
    [InlineData(TestImages.Gif.Issues.Issue2012BadMinCode, 1)]
    public void Identify_Frames_Bad_Lzw(string imagePath, int framesCount)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        GifMetadata gifMetadata = imageInfo.Metadata.GetGifMetadata();
        Assert.NotNull(gifMetadata);

        Assert.Equal(framesCount, imageInfo.FrameMetadataCollection.Count);
        GifFrameMetadata gifFrameMetadata = imageInfo.FrameMetadataCollection[imageInfo.FrameMetadataCollection.Count - 1].GetGifMetadata();

        Assert.NotNull(gifFrameMetadata);
    }
}
