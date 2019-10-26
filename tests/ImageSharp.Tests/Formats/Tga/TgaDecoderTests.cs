// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    using static TestImages.Tga;

    public class TgaDecoderTests
    {
        [Theory]
        [WithFile(Grey, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_MonoChrome<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit15Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_15Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16PalRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24RleTopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24TopLeft, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Palette_WithTopLeftOrigin_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_Uncompressed_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(GreyRle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_MonoChrome<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit32Rle, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_RunLengthEncoded_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit16Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Bit24Pal, PixelTypes.Rgba32)]
        public void TgaDecoder_CanDecode_WithPalette_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TgaDecoder()))
            {
                image.DebugSave(provider);
                TgaTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }
    }
}
