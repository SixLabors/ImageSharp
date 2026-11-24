// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tga;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Tga;

[Trait("Format", "Tga")]
[ValidateDisposedMemoryAllocations]
public class TgaDecoderTests
{
    [Theory]
    [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
    [WithFile(Bit24PalTopLeft, PixelTypes.Rgba32)]
    [WithFile(Bit32PalRleTopLeft, PixelTypes.Rgba32)]
    [WithFile(ColorMapOffset, PixelTypes.Rgba32)]
    public void TgaDecoder_IdentifyReadsColorTable<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageInfo info = Image.Identify(TestFile.Create(provider.SourceFileOrDescription).Bytes);
        TgaMetadata identified = info.Metadata.GetTgaMetadata();
        using Image<TPixel> image = provider.GetImage(TgaDecoder.Instance);
        TgaMetadata decoded = image.Metadata.GetTgaMetadata();

        Assert.Equal(PixelColorType.Indexed, info.PixelType.ColorType);
        Assert.Equal(decoded.ColorMapFirstEntryIndex, identified.ColorMapFirstEntryIndex);
        Assert.Equal(decoded.ColorMapEntryBitDepth, identified.ColorMapEntryBitDepth);
        Assert.NotNull(identified.ColorTable);
        Assert.Equal(decoded.ColorTable.Value.ToArray(), identified.ColorTable.Value.ToArray());
    }

    [Theory]
    [WithFile(ColorMapOffset, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_ColorMapFirstEntryIndex<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TgaDecoder.Instance);
        image.CompareToReferenceOutput(ImageComparer.Exact, provider);

        TgaMetadata metadata = image.Metadata.GetTgaMetadata();

        // The file stores 72 colors beginning at absolute index 39; pixel indices address that range.
        Assert.Equal(39, metadata.ColorMapFirstEntryIndex);
        Assert.Equal(72, metadata.ColorTable.Value.Length);
    }

    [Theory]
    [WithFile(Bit24PalTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_IdentifyPreservesEncodedPaletteColors<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageInfo info = Image.Identify(TestFile.Create(provider.SourceFileOrDescription).Bytes);
        TgaMetadata metadata = info.Metadata.GetTgaMetadata();

        Assert.Equal(0, metadata.ColorMapFirstEntryIndex);
        Assert.Equal(24, metadata.ColorMapEntryBitDepth);
        Assert.Equal(256, metadata.ColorTable.Value.Length);

        // The real fixture begins with BGR bytes 01-00-00, then 09-05-04.
        Assert.Equal(Color.FromPixel(new Bgr24(0, 0, 1)), metadata.ColorTable.Value.Span[0]);
        Assert.Equal(Color.FromPixel(new Bgr24(4, 5, 9)), metadata.ColorTable.Value.Span[1]);
    }

    [Theory]
    [WithFile(Gray8BitTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithTopLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithBottomLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithTopRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithBottomRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitRleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitRleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitRleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray8BitRleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Gray16BitTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithBottomRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Gray_WithTopRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitRleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitRleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitRleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Gray16BitRleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopRightOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);

            // Using here the reference output instead of the the reference decoder,
            // because the reference decoder output seems not to be correct for 16bit gray images.
            image.CompareToReferenceOutput(ImageComparer.Exact, provider);
        }
    }

    [Theory]
    [WithFile(Bit15, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_15Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit15Rle, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_15Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit16BottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24BottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24TopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24BottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24RleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24RleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24RleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Palette_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32TopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32TopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32BottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit16RleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24RleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32RleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32RleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32RleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32RleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalRleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_Paletted_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalRleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_Paletted_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalRleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalRleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_Paletted_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit16PalBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPaletteBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPaletteTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPaletteTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPaletteBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPaletteBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalRleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_WithPaletteTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalRleTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_WithPaletteTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalRleBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_WithPaletteBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit24PalRleBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_RLE_WithPaletteBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPalette_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalBottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPalette_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalBottomRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPalette_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32PalTopRight, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithPalette_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(NoAlphaBits16Bit, PixelTypes.Rgba32)]
    [WithFile(NoAlphaBits16BitRle, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet_16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(NoAlphaBits32Bit, PixelTypes.Rgba32)]
    [WithFile(NoAlphaBits32BitRle, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    // Test case for legacy format, when RLE crosses multiple lines:
    // https://github.com/SixLabors/ImageSharp/pull/2172
    [Theory]
    [WithFile(Github_RLE_legacy, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_LegacyFormat<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit32RleTopLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default)
        {
            TargetSize = new Size { Width = 150, Height = 150 }
        };

        using Image<TPixel> image = provider.GetImage(TgaDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

        // Floating point differences in FMA used in the ResizeKernel result in minor pixel differences.
        // Output have been manually verified.
        // For more details see discussion: https://github.com/SixLabors/ImageSharp/pull/1513#issuecomment-763643594
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(Fma.IsSupported ? 0.0001F : 0.0016F),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2629
    [Theory]
    [WithFile(Issue2629, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_Issue2629<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TgaDecoder.Instance))
        {
            image.DebugSave(provider);
            ImageComparingUtils.CompareWithReferenceDecoder(provider, image);
        }
    }

    [Theory]
    [WithFile(Bit16BottomLeft, PixelTypes.Rgba32)]
    [WithFile(Bit24BottomLeft, PixelTypes.Rgba32)]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(TgaDecoder.Instance));
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    /// <summary>
    /// Verifies that a file that ends in the middle of the pixel data decodes with the default options. The complete
    /// rows match the decode of the full file, and the rows that are not read keep the fill value. In strict mode the
    /// decoder throws.
    /// </summary>
    /// <param name="imagePath">The test image to truncate.</param>
    [Theory]
    [InlineData(Bit24BottomLeft)]
    [InlineData(Bit32TopLeft)]
    [InlineData(Gray8BitTopRight)]
    [InlineData(Bit24RleTopLeft)]
    [InlineData(Bit24PalTopLeft)]
    [InlineData(Bit24PalRleTopLeft)]
    public void TgaDecoder_TruncatedPixelData_KeepsDecodedRows(string imagePath)
    {
        byte[] full = TestFile.Create(imagePath).Bytes;

        // Each file stores its header and color map in the first half, and its pixel data ends in the second half.
        byte[] truncated = full[..(full.Length / 2)];

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, truncated));

        // The decoder keeps each complete pixel in stream order. A partial pixel and the pixels after it keep the cleared value.
        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        ClearPixelsAfterStreamEnd(expected, truncated);

        using Image<Rgba32> actual = Image.Load<Rgba32>(truncated);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Clears the pixels that TGA data that ends early does not hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header is 18 bytes. The image ID and the color map follow it, then the pixel data. The image descriptor at
    /// byte 17 gives the order of the stored pixels. If bit 4 is set, each row goes from right to left. If bit 5 is
    /// clear, the rows go from the bottom up.
    /// </para>
    /// <para>
    /// An RLE packet starts with a repetition count byte. Its low 7 bits store the pixel count minus 1. A run-length
    /// packet has bit 7 set and one pixel value. A raw packet has one pixel value for each pixel.
    /// </para>
    /// <para>
    /// See the Truevision TGA File Format Specification, Version 2.0, fields 1, 4 and 5, and section "Run-Length
    /// Encoding of Images" (src/ImageSharp/Formats/Tga/TGA_Specification.pdf).
    /// </para>
    /// </remarks>
    /// <param name="image">The complete decode. The pixels that the data does not hold are cleared.</param>
    /// <param name="data">The TGA file. It can end inside the pixel data.</param>
    private static void ClearPixelsAfterStreamEnd(Image<Rgba32> image, byte[] data)
    {
        int colorMapLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5));
        int colorMapEntryBytes = (data[7] + 7) / 8;
        int pixelBytes = (data[16] + 7) / 8;
        int offset = 18 + data[0] + (colorMapLength * colorMapEntryBytes);
        int pixelCount = image.Width * image.Height;

        // Image types 9, 10 and 11 are RLE.
        int storedPixels = 0;
        if (data[2] < 9)
        {
            storedPixels = Math.Min(pixelCount, (data.Length - offset) / pixelBytes);
        }
        else
        {
            while (storedPixels < pixelCount && offset < data.Length)
            {
                byte header = data[offset++];
                int packetPixels = (header & 127) + 1;
                if ((header & 128) != 0)
                {
                    if (offset + pixelBytes > data.Length)
                    {
                        break;
                    }

                    offset += pixelBytes;
                    storedPixels += packetPixels;
                }
                else
                {
                    int available = Math.Min(packetPixels, (data.Length - offset) / pixelBytes);
                    offset += available * pixelBytes;
                    storedPixels += available;
                    if (available < packetPixels)
                    {
                        break;
                    }
                }
            }

            storedPixels = Math.Min(storedPixels, pixelCount);
        }

        bool rightToLeft = (data[17] & 0x10) != 0;
        bool bottomUp = (data[17] & 0x20) == 0;
        for (int i = storedPixels; i < pixelCount; i++)
        {
            int row = i / image.Width;
            int column = i % image.Width;
            int x = rightToLeft ? image.Width - 1 - column : column;
            int y = bottomUp ? image.Height - 1 - row : row;
            image[x, y] = default;
        }
    }

    /// <summary>
    /// Verifies that a run length packet that writes past the last pixel is damaged image data. In strict mode the
    /// decoder throws. With the default options the decoder keeps the pixels of the packet that fit in the image.
    /// </summary>
    [Fact]
    public void TgaDecoder_RunLengthPacketPastImageEnd_KeepsPixelsThatFit()
    {
        // A 2x1 24-bit RLE image with one run packet that repeats a red BGR pixel 3 times.
        byte[] data = [0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 1, 0, 24, 0x20, 0x82, 0, 0, 255];

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream strictStream = new(data, false);
        Assert.Throws<InvalidImageContentException>(() => TgaDecoder.Instance.Decode<Rgba32>(strict, strictStream));

        // The packet fills both pixels of the image with red. The third pixel does not fit, so the decoder drops it.
        Rgba32[] pixels = [new(255, 0, 0, 255), new(255, 0, 0, 255)];
        using Image<Rgba32> expected = Image.LoadPixelData<Rgba32>(pixels, 2, 1);
        using MemoryStream defaultStream = new(data, false);
        using Image<Rgba32> image = TgaDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, defaultStream);
        ImageComparer.Exact.VerifySimilarity(expected, image);
    }

    [Theory]
    [WithFile(Bit24BottomLeft, PixelTypes.Rgba32)]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaDecoder_CanDecode_WithLimitedAllocatorBufferCapacity(TestImageProvider<Rgba32> provider)
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<Rgba32> provider = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

            using Image<Rgba32> image = provider.GetImage(TgaDecoder.Instance);
            image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);

            if (TestEnvironment.IsWindows)
            {
                image.CompareToOriginal(provider);
            }
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RemoteExecutor.Invoke(
                RunTest,
                providerDump,
                "Disco")
            .Dispose();
    }
}
