// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
[ValidateDisposedMemoryAllocations]
public class IcoDecoderTests
{
    /// <summary>
    /// Checks per-resource profile retention and pixel equality with direct PNG decoding.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="provider">The icon containing a profiled PNG and four unprofiled BMP entries.</param>
    /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
    /// <param name="profileHandling">The profile retention policy.</param>
    [Theory]
    [WithFile(PillowWithIcc, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, false, ColorProfileHandling.Preserve)]
    [WithFile(PillowWithIcc, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, true, ColorProfileHandling.Preserve)]
    [WithFile(PillowWithIcc, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, false, ColorProfileHandling.Convert)]
    [WithFile(PillowWithIcc, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP, true, ColorProfileHandling.Convert)]
    public void DecodeAndIdentify_RetainResourceIccProfiles<TPixel>(TestImageProvider<TPixel> provider, bool skipMetadata, ColorProfileHandling profileHandling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(provider.Configuration) { SkipMetadata = skipMetadata, ColorProfileHandling = profileHandling };

        // Read the ICO bytes to locate its embedded PNG and identify the complete icon.
        byte[] iconData = TestFile.Create(provider.SourceFileOrDescription).Bytes;

        // The first entry is a profiled 256 x 256 PNG. Decoding it directly gives the expected pixels and retained profile.
        IconDirEntry pngEntry = IconDirEntry.Parse(iconData.AsSpan(IconDir.Size, IconDirEntry.Size));
        using MemoryStream pngStream = new(iconData, (int)pngEntry.ImageOffset, (int)pngEntry.BytesInRes, false);
        using Image<TPixel> pngImage = PngDecoder.Instance.Decode<TPixel>(options, pngStream);

        using Image<TPixel> iconImage = provider.GetImage(IcoDecoder.Instance, options);
        ImageInfo iconInfo = Image.Identify(options, iconData);

        Assert.Equal(5, iconImage.Frames.Count);
        Assert.Equal(5, iconInfo.FrameCount);

        // Preserve retains the profile even when SkipMetadata is set. Convert removes it after converting the pixels.
        if (profileHandling is ColorProfileHandling.Preserve)
        {
            Assert.NotNull(pngImage.Metadata.IccProfile);
        }
        else
        {
            Assert.Null(pngImage.Metadata.IccProfile);
        }

        // The icon image and first frame must follow the same profile retention policy as the directly decoded PNG.
        byte[] expectedProfileData = pngImage.Metadata.IccProfile?.ToByteArray();
        Assert.Equal(expectedProfileData, iconImage.Metadata.IccProfile?.ToByteArray());
        Assert.Equal(expectedProfileData, iconInfo.Metadata.IccProfile?.ToByteArray());
        Assert.Equal(expectedProfileData, iconImage.Frames.RootFrame.Metadata.IccProfile?.ToByteArray());
        Assert.Equal(expectedProfileData, iconInfo.FrameMetadataCollection[0].IccProfile?.ToByteArray());

        // Smaller BMP entries have no profiles. They must not inherit the first PNG entry's profile during container assembly.
        for (int i = 1; i < iconImage.Frames.Count; i++)
        {
            Assert.Null(iconImage.Frames[i].Metadata.IccProfile);
            Assert.Null(iconInfo.FrameMetadataCollection[i].IccProfile);
        }

        // Compare only the first icon frame with the directly decoded PNG. Both fill the 256 x 256 canvas.
        ImageComparer.Exact.VerifySimilarity(pngImage, iconImage, (index, _) => index is 0);

        // Dump sRGB pixels without reattaching the source profile to the PNG output.
        iconImage.Metadata.IccProfile = null;
        iconImage.Frames.RootFrame.Metadata.IccProfile = null;
        iconImage.DebugSaveMultiFrame(provider, $"{profileHandling}_{skipMetadata}", encoder: new PngEncoder { BitDepth = PngBitDepth.Bit16 });
    }

    [Fact]
    public void IcoDetector_RejectsCur()
    {
        TestFile file = TestFile.Create(TestImages.Cur.WindowsMouse);
        IcoImageFormatDetector detector = new();

        Assert.False(detector.TryDetectFormat(file.Bytes, out _));
    }

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
    [WithFile(InvalidBpp, PixelTypes.Rgba32)]
    public void InvalidThrows_InvalidImageContentException(TestImageProvider<Rgba32> provider)
        => Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);
        });

    /// <summary>
    /// Verifies that the color table of an embedded BMP that is larger than the input is rejected before the palette is allocated.
    /// </summary>
    [Fact]
    public void Decode_WithColorTableLargerThanInput_ThrowsBeforeAllocating()
    {
        // 70-byte ICO with one 2x2, 1 bpp BMP entry. The entry has no file header and declares ClrUsed = 0x5C0000FF.
        byte[] payload = Convert.FromHexString(
            "000001000100020200000100010030000000160000002800000002000000040000000100010000000000" +
            "000000000000000000000000FF00005C000000000000000000000000");

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(payload));
        Assert.Throws<InvalidImageContentException>(() => Image.Load(payload));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // The declared color table needs about 6 GB. Rejecting it before the allocation costs far less.
        Assert.True(allocated < 1024 * 1024, $"Identify and Load allocated {allocated} bytes.");
    }

    [Theory]
    [WithFile(InvalidAll, PixelTypes.Rgba32)]
    [WithFile(InvalidCompression, PixelTypes.Rgba32)]
    [WithFile(InvalidRLE4, PixelTypes.Rgba32)]
    [WithFile(InvalidRLE8, PixelTypes.Rgba32)]
    public void InvalidThows_NotSupportedException(TestImageProvider<Rgba32> provider)
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
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance, new DecoderOptions(Configuration.Default) { MaxFrames = 1 });
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

        ImageInfo imageInfo = Image.Identify(new DecoderOptions(Configuration.Default) { MaxFrames = 1 }, stream);

        Assert.Single(imageInfo.FrameMetadataCollection);
    }

    /// <summary>
    /// Verifies that a damaged entry throws in strict mode. With the default options the decoder skips the damaged
    /// entry and keeps the other entries.
    /// </summary>
    [Fact]
    public void TruncatedEntry_FollowsImageDataIntegrityHandling()
    {
        byte[] data = [.. TestFile.Create(Flutter).Bytes];
        ushort entryCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        Assert.True(entryCount > 1);

        // Limit the first resource below the PNG signature length. A bounded child decoder must not read into following data.
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(IconDir.Size + 8), 4);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream strictStream = new(data, false);
        Assert.Throws<InvalidImageContentException>(() => IcoDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        using MemoryStream decodeStream = new(data, false);
        using Image<Rgba32> image = IcoDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, decodeStream);
        Assert.Equal(entryCount - 1, image.Frames.Count);

        using MemoryStream identifyStream = new(data, false);
        ImageInfo info = IcoDecoder.Instance.Identify(DecoderOptions.Default, identifyStream);
        Assert.Equal(entryCount - 1, info.FrameMetadataCollection.Count);
    }

    /// <summary>
    /// Verifies that a multi-entry icon file that ends inside its last image resource decodes with the default options.
    /// Every entry is kept. The complete entries match the decode of the full file, and the last entry keeps the rows
    /// that the data holds. In strict mode the decoder throws.
    /// </summary>
    [Fact]
    public void TruncatedLastResource_KeepsEveryEntry()
    {
        byte[] full = TestFile.Create(MultiSizeMultiBitsB).Bytes;
        ushort entryCount = BinaryPrimitives.ReadUInt16LittleEndian(full.AsSpan(4));
        Assert.True(entryCount > 1);

        // Each directory entry stores BytesInRes at byte 8 and ImageOffset at byte 12. Find the last resource in the file.
        int lastEntry = 0;
        uint lastOffset = 0;
        uint lastLength = 0;
        for (int i = 0; i < entryCount; i++)
        {
            Span<byte> entry = full.AsSpan(IconDir.Size + (i * IconDirEntry.Size), IconDirEntry.Size);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if (offset > lastOffset)
            {
                lastEntry = i;
                lastOffset = offset;
                lastLength = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            }
        }

        // End the file in the middle of the last resource. No other resource follows it.
        byte[] data = full[..(int)(lastOffset + (lastLength / 2))];

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream strictStream = new(data, false);
        Assert.Throws<InvalidImageContentException>(() => IcoDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        // The complete entries match the full decode. The last entry keeps the rows that the data holds.
        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        ClearPixelsAfterResourceEnd(expected.Frames[lastEntry], full.AsSpan((int)lastOffset, (int)lastLength), data.Length - (int)lastOffset);

        using Image<Rgba32> actual = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Changes the decode of a complete uncompressed icon bitmap to the decode of the same bitmap when its data ends early.
    /// The bitmap has a BITMAPINFOHEADER, a color table for 8 or fewer bits per pixel, the color rows, then the 1-bit AND
    /// mask rows. The rows are stored from the bottom up, and each row starts on a 4-byte boundary. The header height is
    /// two times the image height, because it counts the mask rows. See "BITMAPINFOHEADER structure",
    /// https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader. The decoder keeps each
    /// complete color row and clears the rows after it. Missing mask bits are 0, so those pixels are opaque. A 32-bit
    /// bitmap has alpha in its pixels and does not use the mask.
    /// </summary>
    /// <param name="frame">The decode of the complete bitmap. It is changed in place.</param>
    /// <param name="resource">The complete bitmap resource.</param>
    /// <param name="available">The number of bytes of the resource that the decoder gets.</param>
    internal static void ClearPixelsAfterResourceEnd(ImageFrame<Rgba32> frame, ReadOnlySpan<byte> resource, int available)
    {
        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(resource);
        int width = BinaryPrimitives.ReadInt32LittleEndian(resource[4..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(resource[8..]) / 2;
        int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(resource[14..]);
        int colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(resource[32..]);
        Assert.Equal(new Size(width, height), frame.Size);

        int colorTableSize = bitsPerPixel <= 8 ? (colorsUsed == 0 ? 1 << bitsPerPixel : colorsUsed) * 4 : 0;
        int stride = (((width * bitsPerPixel) + 31) / 32) * 4;
        int maskStride = ((width + 31) / 32) * 4;
        int colorStart = headerSize + colorTableSize;
        int maskStart = colorStart + (stride * height);
        int colorRows = Math.Clamp((available - colorStart) / stride, 0, height);

        for (int row = 0; row < height; row++)
        {
            Span<Rgba32> pixels = frame.PixelBuffer.DangerousGetRowSpan(height - 1 - row);
            if (row >= colorRows)
            {
                pixels.Clear();
                continue;
            }

            if (bitsPerPixel > 8)
            {
                continue;
            }

            // Each mask byte holds the bits of 8 pixels.
            int maskBytes = Math.Clamp(available - (maskStart + (row * maskStride)), 0, (width + 7) / 8);
            for (int x = maskBytes * 8; x < width; x++)
            {
                pixels[x].A = byte.MaxValue;
            }
        }
    }

    /// <summary>
    /// Verifies that a non-empty resource cannot start at the exclusive end of the icon stream that contains it. The directory check finds the invalid range
    /// before an entry decodes, so the decoder reports it in each mode.
    /// </summary>
    [Fact]
    public void EntryAtEndOfStream_ThrowsInvalidResourceRange()
    {
        byte[] data = [.. TestFile.Create(Bpp32Size1x1).Bytes];
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4)));

        // ImageOffset is the final DWORD in the sole directory entry. The stream length is an exclusive boundary,
        // so accepting this value would create an empty child stream despite the entry declaring non-empty data.
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(IconDir.Size + 12), (uint)data.Length);
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        using MemoryStream decodeStream = new(data, false);
        InvalidImageContentException decodeException = Assert.Throws<InvalidImageContentException>(
            () => IcoDecoder.Instance.Decode<Rgba32>(strict, decodeStream));

        Assert.Equal("The icon directory contains an invalid image resource range.", decodeException.Message);

        using MemoryStream identifyStream = new(data, false);
        InvalidImageContentException identifyException = Assert.Throws<InvalidImageContentException>(
            () => IcoDecoder.Instance.Identify(strict, identifyStream));

        Assert.Equal("The icon directory contains an invalid image resource range.", identifyException.Message);

        using MemoryStream defaultStream = new(data, false);
        Assert.Throws<InvalidImageContentException>(() => IcoDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, defaultStream));
    }

    [Fact]
    public void IcoFrameMetadata_ScalesZeroEncodingDimensionsFrom256()
    {
        using Image<Rgba32> source = new(256, 256);
        using Image<Rgba32> destination = new(128, 128);
        IcoFrameMetadata metadata = new() { EncodingWidth = 0, EncodingHeight = 0 };

        metadata.AfterFrameApply(source.Frames.RootFrame, destination.Frames.RootFrame, Matrix4x4.Identity);

        Assert.Equal((byte)128, metadata.EncodingWidth);
        Assert.Equal((byte)128, metadata.EncodingHeight);
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

    /// <summary>
    /// An icon bitmap must be BI_RGB, which has no 5-6-5 layout. The decoder gives a 5-6-5 entry the 24-bit layout in the
    /// metadata, so a re-encode keeps each bit of the decoded pixels.
    /// </summary>
    [Fact]
    public void DecodeAndIdentify_Bgr565BitmapEntry_RecordsLosslessLayout()
    {
        using Image<Rgba32> source = CreateGradient();
        byte[] data = CreateIconWithRgb565Bitmap(1, source);

        ImageInfo info = Image.Identify(data);
        using Image<Rgba32> image = Image.Load<Rgba32>(data);
        using Image<Bgr565> quantized = source.CloneAs<Bgr565>();
        using Image<Rgba32> expected = quantized.CloneAs<Rgba32>();

        IcoFrameMetadata identified = info.FrameMetadataCollection[0].GetIcoMetadata();
        IcoFrameMetadata decoded = image.Frames.RootFrame.Metadata.GetIcoMetadata();

        Assert.Equal(BmpBitsPerPixel.Bit24, identified.BmpBitsPerPixel);
        Assert.Equal(BmpBitsPerPixel.Bit24, decoded.BmpBitsPerPixel);
        Assert.Equal(BmpColorType.Bgr, identified.BmpColorType);
        Assert.Equal(BmpColorType.Bgr, decoded.BmpColorType);
        Assert.Equal(BmpColorType.Bgr, image.Metadata.GetIcoMetadata().BmpColorType);
        ImageComparer.Exact.VerifySimilarity(expected, image);
    }

    /// <summary>
    /// Creates an opaque image with a different color in each pixel.
    /// </summary>
    /// <returns>The image.</returns>
    internal static Image<Rgba32> CreateGradient()
    {
        Image<Rgba32> image = new(16, 16);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32((byte)(x * 17), (byte)(y * 17), (byte)((x + y) * 8), byte.MaxValue);
            }
        }

        return image;
    }

    /// <summary>
    /// Creates an icon or cursor file with one 16-bit bitmap entry that stores the 5-6-5 bit-field layout. The icon
    /// format permits only BI_RGB, but the decoder reads bit-field entries that other encoders write.
    /// </summary>
    /// <param name="type">The directory type: 1 for an icon and 2 for a cursor.</param>
    /// <param name="source">The pixels of the entry.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateIconWithRgb565Bitmap(ushort type, Image<Rgba32> source)
    {
        // These are the bitmap options of an icon entry: no file header, the doubled height and the AND mask. The 5-6-5
        // layout gives a V3 header with BI_BITFIELDS and the three masks after it.
        using MemoryStream dib = new();
        source.Save(dib, new BmpEncoder
        {
            BitsPerPixel = BmpBitsPerPixel.Bit16,
            ColorType = BmpColorType.Bgr565,
            SkipFileHeader = true,
            UseDoubleHeight = true,
            ProcessedAlphaMask = true
        });

        // The ICONDIR is reserved, type and count. The one ICONDIRENTRY is width, height, color count, reserved, planes
        // or hotspot x, bit count or hotspot y, resource size and resource offset.
        byte[] dibBytes = dib.ToArray();
        byte[] data = new byte[22 + dibBytes.Length];
        Span<byte> span = data;
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], type);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], 1);
        data[6] = (byte)source.Width;
        data[7] = (byte)source.Height;
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 16);
        BinaryPrimitives.WriteUInt32LittleEndian(span[14..], (uint)dibBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[18..], 22);
        dibBytes.CopyTo(span[22..]);

        return data;
    }
}
