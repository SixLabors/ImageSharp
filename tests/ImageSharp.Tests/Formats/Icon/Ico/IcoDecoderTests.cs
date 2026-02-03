// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
[ValidateDisposedMemoryAllocations]
public class IcoDecoderTests
{
    [Theory]
    [WithFile(Flutter, PixelTypes.Rgba32)]
    public void IcoDecoder_Decode(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSaveMultiFrame(provider);

        Assert.Equal(10, image.Frames.Count);
    }

    [Theory]
    [WithFile(Bpp1Size15x15, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size16x16, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size17x17, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size1x1, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size256x256, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size2x2, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size31x31, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size32x32, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size33x33, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size3x3, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size4x4, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size5x5, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size6x6, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size7x7, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size8x8, PixelTypes.Rgba32)]
    [WithFile(Bpp1Size9x9, PixelTypes.Rgba32)]
    [WithFile(Bpp1TranspNotSquare, PixelTypes.Rgba32)]
    [WithFile(Bpp1TranspPartial, PixelTypes.Rgba32)]
    public void Bpp1Test(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit1, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(Bpp24Size15x15, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size16x16, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size17x17, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size1x1, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size256x256, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size2x2, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size31x31, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size32x32, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size33x33, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size3x3, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size4x4, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size5x5, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size6x6, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size7x7, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size8x8, PixelTypes.Rgba32)]
    [WithFile(Bpp24Size9x9, PixelTypes.Rgba32)]
    [WithFile(Bpp24TranspNotSquare, PixelTypes.Rgba32)]
    [WithFile(Bpp24TranspPartial, PixelTypes.Rgba32)]
    [WithFile(Bpp24Transp, PixelTypes.Rgba32)]
    public void Bpp24Test(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit24, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(Bpp32Size15x15, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size16x16, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size17x17, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size1x1, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size256x256, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size2x2, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size31x31, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size32x32, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size33x33, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size3x3, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size4x4, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size5x5, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size6x6, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size7x7, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size8x8, PixelTypes.Rgba32)]
    [WithFile(Bpp32Size9x9, PixelTypes.Rgba32)]
    [WithFile(Bpp32TranspNotSquare, PixelTypes.Rgba32)]
    [WithFile(Bpp32TranspPartial, PixelTypes.Rgba32)]
    [WithFile(Bpp32Transp, PixelTypes.Rgba32)]
    public void Bpp32Test(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(Bpp4Size15x15, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size16x16, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size17x17, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size1x1, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size256x256, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size2x2, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size31x31, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size32x32, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size33x33, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size3x3, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size4x4, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size5x5, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size6x6, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size7x7, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size8x8, PixelTypes.Rgba32)]
    [WithFile(Bpp4Size9x9, PixelTypes.Rgba32)]
    [WithFile(Bpp4TranspNotSquare, PixelTypes.Rgba32)]
    [WithFile(Bpp4TranspPartial, PixelTypes.Rgba32)]
    public void Bpp4Test(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit4, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(Bpp8Size15x15, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size16x16, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size17x17, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size1x1, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size256x256, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size2x2, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size31x31, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size32x32, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size33x33, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size3x3, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size4x4, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size5x5, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size6x6, PixelTypes.Rgba32)]
    [WithFile(Bpp8Size7x7, PixelTypes.Rgba32)]

    // [WithFile(Bpp8Size8x8, PixelTypes.Rgba32)] This is actually 24 bit.
    [WithFile(Bpp8Size9x9, PixelTypes.Rgba32)]
    [WithFile(Bpp8TranspNotSquare, PixelTypes.Rgba32)]
    [WithFile(Bpp8TranspPartial, PixelTypes.Rgba32)]
    public void Bpp8Test(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit8, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(InvalidAll, PixelTypes.Rgba32)]
    [WithFile(InvalidBpp, PixelTypes.Rgba32)]
    [WithFile(InvalidCompression, PixelTypes.Rgba32)]
    [WithFile(InvalidRLE4, PixelTypes.Rgba32)]
    [WithFile(InvalidRLE8, PixelTypes.Rgba32)]
    public void InvalidTest(TestImageProvider<Rgba32> provider)
        => Assert.Throws<NotSupportedException>(() =>
        {
            using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);
        });

    [Theory]
    [WithFile(InvalidPng, PixelTypes.Rgba32)]
    public void InvalidPngTest(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Png, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
    }

    [Theory]
    [WithFile(MixedBmpPngA, PixelTypes.Rgba32)]
    [WithFile(MixedBmpPngB, PixelTypes.Rgba32)]
    [WithFile(MixedBmpPngC, PixelTypes.Rgba32)]
    public void MixedBmpPngTest(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        Assert.True(image.Frames.Count > 1);

        image.DebugSaveMultiFrame(provider);
    }

    [Theory]
    [WithFile(MultiSizeA, PixelTypes.Rgba32)]
    [WithFile(MultiSizeB, PixelTypes.Rgba32)]
    [WithFile(MultiSizeC, PixelTypes.Rgba32)]
    [WithFile(MultiSizeD, PixelTypes.Rgba32)]
    [WithFile(MultiSizeE, PixelTypes.Rgba32)]
    [WithFile(MultiSizeF, PixelTypes.Rgba32)]
    public void MultiSizeTest(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        Assert.True(image.Frames.Count > 1);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            ImageFrame<Rgba32> frame = image.Frames[i];
            IcoFrameMetadata meta = frame.Metadata.GetIcoMetadata();
            Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
        }

        image.DebugSaveMultiFrame(provider);
    }

    [Theory]
    [WithFile(MultiSizeA, PixelTypes.Rgba32)]
    [WithFile(MultiSizeB, PixelTypes.Rgba32)]
    [WithFile(MultiSizeC, PixelTypes.Rgba32)]
    [WithFile(MultiSizeD, PixelTypes.Rgba32)]
    [WithFile(MultiSizeE, PixelTypes.Rgba32)]
    [WithFile(MultiSizeF, PixelTypes.Rgba32)]
    public void MultiSize_CanDecodeSingleFrame(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance, new DecoderOptions { MaxFrames = 1 });
        Assert.Single(image.Frames);
    }

    [Theory]
    [InlineData(MultiSizeA)]
    [InlineData(MultiSizeB)]
    [InlineData(MultiSizeC)]
    [InlineData(MultiSizeD)]
    [InlineData(MultiSizeE)]
    [InlineData(MultiSizeF)]
    public void MultiSize_CanIdentifySingleFrame(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(new DecoderOptions { MaxFrames = 1 }, stream);

        Assert.Single(imageInfo.FrameMetadataCollection);
    }

    [Theory]
    [WithFile(MultiSizeMultiBitsA, PixelTypes.Rgba32)]
    [WithFile(MultiSizeMultiBitsB, PixelTypes.Rgba32)]
    [WithFile(MultiSizeMultiBitsC, PixelTypes.Rgba32)]
    [WithFile(MultiSizeMultiBitsD, PixelTypes.Rgba32)]
    public void MultiSizeMultiBitsTest(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        Assert.True(image.Frames.Count > 1);

        image.DebugSaveMultiFrame(provider);
    }

    [Theory]
    [WithFile(IcoFake, PixelTypes.Rgba32)]
    public void IcoFakeTest(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider);

        IcoFrameMetadata meta = image.Frames.RootFrame.Metadata.GetIcoMetadata();
        int expectedWidth = image.Width >= 256 ? 0 : image.Width;
        int expectedHeight = image.Height >= 256 ? 0 : image.Height;

        Assert.Equal(expectedWidth, meta.EncodingWidth.Value);
        Assert.Equal(expectedHeight, meta.EncodingHeight.Value);
        Assert.Equal(IconFrameCompression.Bmp, meta.Compression);
        Assert.Equal(BmpBitsPerPixel.Bit32, meta.BmpBitsPerPixel);
    }
}
