// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.DotNet.RemoteExecutor;

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tga;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    [Trait("Format", "Tga")]
    public class TgaDecoderTests
    {
        private static TgaDecoder TgaDecoder => new TgaDecoder();

        [Theory]
        [WithFile(Gray8BitTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithTopLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithBottomLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithTopRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_WithBottomRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitRleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitRleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithTopRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitRleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomLeftOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray8BitRleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_Gray_WithBottomRightOrigin_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Gray16BitTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Gray_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
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
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16BottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24BottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24BottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Palette_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32TopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32BottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16RleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32RleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalRleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_Paletted_WithTopLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalRleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_Paletted_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalRleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalRleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_Paletted_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16PalBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPaletteBottomLeftOrigin_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPaletteTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPaletteTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPaletteBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPaletteBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalRleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_WithPaletteTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalRleTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_WithPaletteTopRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalRleBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_WithPaletteBottomLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24PalRleBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RLE_WithPaletteBottomRightOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalBottomLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithBottomLeftOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalBottomRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithBottomRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32PalTopRight, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_WithTopRightOrigin_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(NoAlphaBits16Bit, PixelTypes.Rgba32)]
        [WithFile(NoAlphaBits16BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(NoAlphaBits32Bit, PixelTypes.Rgba32)]
        [WithFile(NoAlphaBits32BitRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WhenAlphaBitsNotSet<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TgaDecoder))
            {
                // Using here the reference output instead of the the reference decoder,
                // because the reference decoder does not ignore the alpha data here.
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ImageComparer.Exact, provider);
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
            InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(TgaDecoder));
            Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
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

                using Image<Rgba32> image = provider.GetImage(TgaDecoder);
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
}
