// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    using SixLabors.ImageSharp.Metadata;
    using static TestImages.Bmp;

    public class BmpDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly string[] MiscBmpFiles = Miscellaneous;

        public static readonly string[] BitfieldsBmpFiles = BitFields;

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        [Theory]
        [WithFileCollection(nameof(MiscBmpFiles), PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_MiscellaneousBitmaps<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFileCollection(nameof(BitfieldsBmpFiles), PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBitfields<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit16Inverted, PixelTypes.Rgba32)]
        [WithFile(Bit8Inverted, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_Inverted<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit1, PixelTypes.Rgba32)]
        [WithFile(Bit1Pal1, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_1Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new SystemDrawingReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Bit4, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_4Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                // The Magick Reference Decoder can not decode 4-Bit bitmaps, so only execute this on windows.
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(Bit8, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit16, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_32Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Rgba32v4, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_32BitV4Header_Fast<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(RLE4Cut, PixelTypes.Rgba32)]
        [WithFile(RLE4Delta, PixelTypes.Rgba32)]
        [WithFile(Rle4Delta320240, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_4Bit_WithDelta<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.Black }))
            {
                image.DebugSave(provider);
                // The Magick Reference Decoder can not decode 4-Bit bitmaps, so only execute this on windows.
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(RLE4, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_4Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.Black }))
            {
                image.DebugSave(provider);
                // The Magick Reference Decoder can not decode 4-Bit bitmaps, so only execute this on windows.
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(RLE8Cut, PixelTypes.Rgba32)]
        [WithFile(RLE8Delta, PixelTypes.Rgba32)]
        [WithFile(Rle8Delta320240, PixelTypes.Rgba32)]
        [WithFile(Rle8Blank160120, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_WithDelta_SystemDrawingRefDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.Black }))
            {
                image.DebugSave(provider);
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider, new SystemDrawingReferenceDecoder());
                }
            }
        }

        [Theory]
        [WithFile(RLE8Cut, PixelTypes.Rgba32)]
        [WithFile(RLE8Delta, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit_WithDelta_MagickRefDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette }))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(RLE8, PixelTypes.Rgba32)]
        [WithFile(RLE8Inverted, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_8Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.FirstColorOfPalette }))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(RLE24, PixelTypes.Rgba32)]
        [WithFile(RLE24Cut, PixelTypes.Rgba32)]
        [WithFile(RLE24Delta, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_RunLengthEncoded_24Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder { RleSkippedPixelHandling = RleSkippedPixelHandling.Black }))
            {
                image.DebugSave(provider);

                // TODO: Neither System.Drawing nor MagickReferenceDecoder decode this file.
                // image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(RgbaAlphaBitfields, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeAlphaBitfields<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);

                // TODO: Neither System.Drawing nor MagickReferenceDecoder decode this file.
                // image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit32Rgba, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBitmap_WithAlphaChannel<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Rgba321010102, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBitfields_WithUnusualBitmasks<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);

                // Choosing large tolerance of 6.1 here, because for some reason with the MagickReferenceDecoder the alpha channel
                // seems to be wrong. This bitmap has an alpha channel of two bits. In many cases this alpha channel has a value of 3,
                // which should be remapped to 255 for RGBA32, but the magick decoder has a value of 191 set.
                // The total difference without the alpha channel is still: 0.0204%
                // Exporting the image as PNG with GIMP yields to the same result as the ImageSharp implementation.
                image.CompareToOriginal(provider, ImageComparer.TolerantPercentage(6.1f), new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(WinBmpv2, PixelTypes.Rgba32)]
        [WithFile(CoreHeader, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBmpv2<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(WinBmpv3, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBmpv3<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(LessThanFullSizedPalette, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeLessThanFullPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(OversizedPalette, PixelTypes.Rgba32)]
        [WithFile(Rgb24LargePalette, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeOversizedPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(InvalidPaletteSize, PixelTypes.Rgba32)]
        public void BmpDecoder_ThrowsImageFormatException_OnInvalidPaletteSize<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Throws<ImageFormatException>( () => { using (provider.GetImage(new BmpDecoder())) { } });
        }

        [Theory]
        [WithFile(Rgb24jpeg, PixelTypes.Rgba32)]
        [WithFile(Rgb24png, PixelTypes.Rgba32)]
        public void BmpDecoder_ThrowsNotSupportedException_OnUnsupportedBitmaps<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.Throws<NotSupportedException>(() => { using (provider.GetImage(new BmpDecoder())) { } });
        }

        [Theory]
        [WithFile(Rgb32h52AdobeV3, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeAdobeBmpv3<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Rgba32bf56AdobeV3, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeAdobeBmpv3_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(WinBmpv4, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBmpv4<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(WinBmpv5, PixelTypes.Rgba32)]
        [WithFile(V5Header, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecodeBmpv5<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Pal8Offset, PixelTypes.Rgba32)]
        public void BmpDecoder_RespectsFileHeaderOffset<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(F, CommonNonDefaultPixelTypes)]
        public void BmpDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Bit8Palette4, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode4BytePerEntryPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [InlineData(Bit32Rgb, 32)]
        [InlineData(Bit32Rgba, 32)]
        [InlineData(Car, 24)]
        [InlineData(F, 24)]
        [InlineData(NegHeight, 24)]
        [InlineData(Bit16, 16)]
        [InlineData(Bit16Inverted, 16)]
        [InlineData(Bit8, 8)]
        [InlineData(Bit8Inverted, 8)]
        [InlineData(Bit4, 4)]
        [InlineData(Bit1, 1)]
        [InlineData(Bit1Pal1, 1)]
        public void Identify_DetectsCorrectPixelType(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(expectedPixelSize, imageInfo.PixelType?.BitsPerPixel);
            }
        }

        [Theory]
        [InlineData(Bit32Rgb, 127, 64)]
        [InlineData(Car, 600, 450)]
        [InlineData(Bit16, 127, 64)]
        [InlineData(Bit16Inverted, 127, 64)]
        [InlineData(Bit8, 127, 64)]
        [InlineData(Bit8Inverted, 127, 64)]
        [InlineData(RLE8, 491, 272)]
        [InlineData(RLE8Inverted, 491, 272)]
        public void Identify_DetectsCorrectWidthAndHeight(string imagePath, int expectedWidth, int expectedHeight)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(expectedWidth, imageInfo.Width);
                Assert.Equal(expectedHeight, imageInfo.Height);
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new BmpDecoder();
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, stream))
                {
                    ImageMetadata meta = image.Metadata;
                    Assert.Equal(xResolution, meta.HorizontalResolution);
                    Assert.Equal(yResolution, meta.VerticalResolution);
                    Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                }
            }
        }

        [Theory]
        [WithFile(Os2v2Short, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_Os2v2XShortHeader<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);

                // TODO: Neither System.Drawing or MagickReferenceDecoder can correctly decode this file.
                // image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [WithFile(Os2v2, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_Os2v2Header<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);

                // TODO: System.Drawing can not decode this image. MagickReferenceDecoder can decode it,
                // but i think incorrectly. I have loaded the image with GIMP and exported as PNG.
                // The results are the same as the image sharp implementation.
                // image.CompareToOriginal(provider, new MagickReferenceDecoder());
            }
        }

        [Theory]
        [WithFile(Os2BitmapArray, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArray9s, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArrayDiamond, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArraySkater, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArraySpade, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArraySunflower, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArrayMarble, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArrayWarpd, PixelTypes.Rgba32)]
        [WithFile(Os2BitmapArrayPines, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_Os2BitmapArray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider);

                // TODO: Neither System.Drawing or MagickReferenceDecoder can correctly decode this file.
                // image.CompareToOriginal(provider);
            }
        }
    }
}
