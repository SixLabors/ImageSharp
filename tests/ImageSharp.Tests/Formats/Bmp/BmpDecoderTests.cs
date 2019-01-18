// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.MetaData;
    using static TestImages.Bmp;

    public class BmpDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly string[] AllBmpFiles = All;

        public static readonly string[] BitfieldsBmpFiles = BitFields;

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Bmp.Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.RLE, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32)]
        public void DecodeBmp<TPixel>(TestImageProvider<TPixel> provider)
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
                // Exporting the image as PNG with GIMP yields to the same result as the imagesharp implementation.
                image.CompareToOriginal(provider, ImageComparer.TolerantPercentage(6.1f), new MagickReferenceDecoder());                
            }
        }

        [Theory]
        [WithFile(WinBmpv2, PixelTypes.Rgba32)]
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
        [WithFile(Rgba32bf56, PixelTypes.Rgba32)]
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
        [InlineData(Car, 24)]
        [InlineData(F, 24)]
        [InlineData(NegHeight, 24)]
        [InlineData(Bit8, 8)]
        [InlineData(Bit8Inverted, 8)]
        public void Identify(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.Identify(stream)?.PixelType?.BitsPerPixel);
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
                    ImageMetaData meta = image.MetaData;
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
                image.DebugSave(provider, "png");

                // TODO: Neither System.Drawing not MagickReferenceDecoder 
                // can correctly decode this file.
                // image.CompareToOriginal(provider);
            }
        }
    }
}