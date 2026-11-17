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

    /// <summary>
    /// Verifies that the color table of an embedded BMP that is larger than the input is rejected before the palette is allocated.
    /// </summary>
    [Fact]
    public void Decode_WithColorTableLargerThanInput_ThrowsBeforeAllocating()
    {
        // 70-byte CUR with one 2x2, 1 bpp BMP entry. The entry has no file header and declares ClrUsed = 0x5C0000FF.
        byte[] payload = Convert.FromHexString(
            "000002000100020200000100010030000000160000002800000002000000040000000100010000000000" +
            "000000000000000000000000FF00005C000000000000000000000000");

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(payload));
        Assert.Throws<InvalidImageContentException>(() => Image.Load(payload));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // The declared color table needs about 6 GB. Rejecting it before the allocation costs far less.
        Assert.True(allocated < 1024 * 1024, $"Identify and Load allocated {allocated} bytes.");
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
