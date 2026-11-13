// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Icon.Ico;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Cur;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Cur;

[Trait("Format", "Cur")]
[ValidateDisposedMemoryAllocations]
public class CurDecoderTests
{
    [Fact]
    public void CurFormat_HasCorrectName()
        => Assert.Equal("CUR", CurFormat.Instance.Name);

    [Fact]
    public void CurDetector_RejectsIco()
    {
        TestFile file = TestFile.Create(TestImages.Ico.Flutter);
        CurImageFormatDetector detector = new();

        Assert.False(detector.TryDetectFormat(file.Bytes, out _));
    }

    [Theory]
    [WithFile(WindowsMouse, PixelTypes.Rgba32)]
    public void CurDecoder_Decode(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(CurDecoder.Instance);

        CurFrameMetadata meta = image.Frames[0].Metadata.GetCurMetadata();
        Assert.Equal(image.Width, meta.EncodingWidth.Value);
        Assert.Equal(image.Height, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(CurFake, PixelTypes.Rgba32)]
    [WithFile(CurReal, PixelTypes.Rgba32)]
    public void CurDecoder_Decode2(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(CurDecoder.Instance);
        CurFrameMetadata meta = image.Frames[0].Metadata.GetCurMetadata();
        Assert.Equal(image.Width, meta.EncodingWidth.Value);
        Assert.Equal(image.Height, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
    }

    [Fact]
    public void CurFrameMetadata_DeepClonePreservesColorTable()
    {
        Color[] colors = [Color.Red, Color.Green];
        CurFrameMetadata metadata = new() { ColorTable = colors };

        CurFrameMetadata clone = metadata.DeepClone();
        colors[0] = Color.Blue;

        Assert.Equal(Color.Red, clone.ColorTable.Value.Span[0]);
        Assert.Equal(Color.Green, clone.ColorTable.Value.Span[1]);
    }

    [Fact]
    public void CurFrameMetadata_ScalesZeroEncodingDimensionsFrom256()
    {
        using Image<Rgba32> source = new(256, 256);
        using Image<Rgba32> destination = new(128, 128);
        CurFrameMetadata metadata = new() { EncodingWidth = 0, EncodingHeight = 0 };

        metadata.AfterFrameApply(source.Frames.RootFrame, destination.Frames.RootFrame, Matrix4x4.Identity);

        Assert.Equal((byte)128, metadata.EncodingWidth);
        Assert.Equal((byte)128, metadata.EncodingHeight);
    }

    [Fact]
    public void DecodeAndIdentify_Bgr565BitmapEntry_RecordsColorType()
    {
        using Image<Rgba32> source = IcoDecoderTests.CreateGradient();
        byte[] data = IcoDecoderTests.CreateIconWithRgb565Bitmap(2, source);

        ImageInfo info = Image.Identify(data);
        using Image<Rgba32> image = Image.Load<Rgba32>(data);
        using Image<Bgr565> quantized = source.CloneAs<Bgr565>();
        using Image<Rgba32> expected = quantized.CloneAs<Rgba32>();

        CurFrameMetadata identified = info.FrameMetadataCollection[0].GetCurMetadata();
        CurFrameMetadata decoded = image.Frames.RootFrame.Metadata.GetCurMetadata();

        Assert.Equal(BmpBitsPerPixel.Bit16, decoded.BmpBitsPerPixel);
        Assert.Equal(BmpColorType.Bgr565, identified.BmpColorType);
        Assert.Equal(BmpColorType.Bgr565, decoded.BmpColorType);
        Assert.Equal(BmpColorType.Bgr565, image.Metadata.GetCurMetadata().BmpColorType);
        Assert.Equal(6, decoded.ToFormatConnectingFrameMetadata().PixelTypeInfo!.Value.ComponentInfo!.Value.GetComponentPrecision(1));
        ImageComparer.Exact.VerifySimilarity(expected, image);
    }
}
