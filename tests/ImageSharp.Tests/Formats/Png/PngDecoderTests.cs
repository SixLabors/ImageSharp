// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public partial class PngDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.Splash,
            TestImages.Png.Indexed,
            TestImages.Png.FilterVar,
            TestImages.Png.Bad.ChunkLength1,
            TestImages.Png.Bad.CorruptedChunk,

            TestImages.Png.VimImage1,
            TestImages.Png.VersioningImage1,
            TestImages.Png.VersioningImage2,

            TestImages.Png.SnakeGame,
            TestImages.Png.Banner7Adam7InterlaceMode,
            TestImages.Png.Banner8Index,

            TestImages.Png.Bad.ChunkLength2,
            TestImages.Png.VimImage2,

            TestImages.Png.Rgb24BppTrans,
            TestImages.Png.GrayA8Bit,
            TestImages.Png.Gray1BitTrans,
            TestImages.Png.Bad.ZlibOverflow,
            TestImages.Png.Bad.ZlibOverflow2,
            TestImages.Png.Bad.ZlibZtxtBadHeader,
            TestImages.Png.Bad.Issue1047_BadEndChunk
        };

        public static readonly string[] TestImages48Bpp =
        {
            TestImages.Png.Rgb48Bpp,
            TestImages.Png.Rgb48BppInterlaced
        };

        public static readonly string[] TestImages64Bpp =
{
            TestImages.Png.Rgba64Bpp,
            TestImages.Png.Rgb48BppTrans
        };

        public static readonly string[] TestImagesL16Bit =
        {
            TestImages.Png.L16Bit,
        };

        public static readonly string[] TestImagesGrayAlpha16Bit =
        {
            TestImages.Png.GrayAlpha16Bit,
            TestImages.Png.GrayTrns16BitInterlaced
        };

        public static readonly string[] TestImagesL8BitInterlaced =
            {
                TestImages.Png.GrayAlpha1BitInterlaced,
                TestImages.Png.GrayAlpha2BitInterlaced,
                TestImages.Png.Gray4BitInterlaced,
                TestImages.Png.GrayA8BitInterlaced
            };

        public static readonly string[] TestImagesIssue1014 =
        {
            TestImages.Png.Issue1014_1, TestImages.Png.Issue1014_2,
            TestImages.Png.Issue1014_3, TestImages.Png.Issue1014_4,
            TestImages.Png.Issue1014_5, TestImages.Png.Issue1014_6
        };

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);

                // We don't have another x-plat reference decoder that can be compared for this image.
                if (provider.Utility.SourceFileOrDescription == TestImages.Png.Bad.Issue1047_BadEndChunk)
                {
                    if (TestEnvironment.IsWindows)
                    {
                        image.CompareToOriginal(provider, ImageComparer.Exact, (IImageDecoder)SystemDrawingReferenceDecoder.Instance);
                    }
                }
                else
                {
                    image.CompareToOriginal(provider, ImageComparer.Exact);
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Interlaced, PixelTypes.Rgba32)]
        public void Decode_Interlaced_ImageIsCorrect<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImages48Bpp), PixelTypes.Rgb48)]
        public void Decode_48Bpp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImages64Bpp), PixelTypes.Rgba64)]
        public void Decode_64Bpp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImagesL8BitInterlaced), PixelTypes.Rgba32)]
        public void Decoder_L8bitInterlaced<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImagesL16Bit), PixelTypes.Rgb48)]
        public void Decode_L16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImagesGrayAlpha16Bit), PixelTypes.Rgba64)]
        public void Decode_GrayAlpha16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayA8BitInterlaced, PixelTypes)]
        public void Decoder_CanDecodeGrey8bitWithAlpha<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Splash, PixelTypes)]
        public void Decoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [InlineData(TestImages.Png.Bpp1, 1)]
        [InlineData(TestImages.Png.Gray4Bpp, 4)]
        [InlineData(TestImages.Png.Palette8Bpp, 8)]
        [InlineData(TestImages.Png.Pd, 24)]
        [InlineData(TestImages.Png.Blur, 32)]
        [InlineData(TestImages.Png.Rgb48Bpp, 48)]
        [InlineData(TestImages.Png.Rgb48BppInterlaced, 48)]
        public void Identify(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.Identify(stream)?.PixelType?.BitsPerPixel);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImagesIssue1014), PixelTypes.Rgba32)]
        public void Issue1014<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
                    {
                        image.DebugSave(provider);
                        image.CompareToOriginal(provider, ImageComparer.Exact);
                    }
                });
            Assert.Null(ex);
        }
    }
}
