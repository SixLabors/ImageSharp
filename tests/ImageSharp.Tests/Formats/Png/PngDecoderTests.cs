// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using Microsoft.DotNet.RemoteExecutor;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public partial class PngDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        private static PngDecoder PngDecoder => new PngDecoder();

        public static readonly string[] CommonTestImages =
        {
            TestImages.Png.Splash,
            TestImages.Png.FilterVar,

            TestImages.Png.VimImage1,
            TestImages.Png.VimImage2,
            TestImages.Png.VersioningImage1,
            TestImages.Png.VersioningImage2,

            TestImages.Png.SnakeGame,

            TestImages.Png.Rgb24BppTrans,

            TestImages.Png.Bad.ChunkLength1,
            TestImages.Png.Bad.ChunkLength2,
        };

        public static readonly string[] TestImagesIssue1014 =
        {
            TestImages.Png.Issue1014_1, TestImages.Png.Issue1014_2,
            TestImages.Png.Issue1014_3, TestImages.Png.Issue1014_4,
            TestImages.Png.Issue1014_5, TestImages.Png.Issue1014_6
        };

        public static readonly string[] TestImagesIssue1177 =
        {
            TestImages.Png.Issue1177_1,
            TestImages.Png.Issue1177_2
        };

        public static readonly string[] CorruptedTestImages =
        {
            TestImages.Png.Bad.CorruptedChunk,
            TestImages.Png.Bad.ZlibOverflow,
            TestImages.Png.Bad.ZlibOverflow2,
            TestImages.Png.Bad.ZlibZtxtBadHeader,
        };

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayA8Bit, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Gray1BitTrans, PixelTypes.Rgba32)]
        public void Decode_GrayWithAlpha<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Interlaced, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Banner7Adam7InterlaceMode, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Banner8Index, PixelTypes.Rgba32)]
        public void Decode_Interlaced<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Indexed, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Banner8Index, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.PalettedTwoColor, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.PalettedFourColor, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.PalettedSixteenColor, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Paletted256Colors, PixelTypes.Rgba32)]
        public void Decode_Indexed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgb48)]
        [WithFile(TestImages.Png.Rgb48BppInterlaced, PixelTypes.Rgb48)]
        public void Decode_48Bpp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Rgba64Bpp, PixelTypes.Rgba64)]
        [WithFile(TestImages.Png.Rgb48BppTrans, PixelTypes.Rgba64)]
        public void Decode_64Bpp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayAlpha1BitInterlaced, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.GrayAlpha2BitInterlaced, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Gray4BitInterlaced, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.GrayA8BitInterlaced, PixelTypes.Rgba32)]
        public void Decoder_L8bitInterlaced<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.L16Bit, PixelTypes.Rgb48)]
        public void Decode_L16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayAlpha16Bit, PixelTypes.Rgba64)]
        [WithFile(TestImages.Png.GrayTrns16BitInterlaced, PixelTypes.Rgba64)]
        public void Decode_GrayAlpha16Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayA8BitInterlaced, PixelTypes)]
        public void Decoder_CanDecode_Grey8bitInterlaced_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFileCollection(nameof(CorruptedTestImages), PixelTypes.Rgba32)]
        public void Decoder_CanDecode_CorruptedImages<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            }
        }

        [Theory]
        [WithFile(TestImages.Png.Splash, PixelTypes)]
        public void Decoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(PngDecoder))
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
        [WithFile(TestImages.Png.Bad.MissingDataChunk, PixelTypes.Rgba32)]
        public void Decode_MissingDataChunk_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                    }
                });
            Assert.NotNull(ex);
            Assert.Contains("PNG Image does not contain a data chunk", ex.Message);
        }

        [Theory]
        [WithFile(TestImages.Png.Bad.BitDepthZero, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bad.BitDepthThree, PixelTypes.Rgba32)]
        public void Decode_InvalidBitDepth_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                    }
                });
            Assert.NotNull(ex);
            Assert.Contains("Invalid or unsupported bit depth", ex.Message);
        }

        [Theory]
        [WithFile(TestImages.Png.Bad.ColorTypeOne, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bad.ColorTypeNine, PixelTypes.Rgba32)]
        public void Decode_InvalidColorType_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                    }
                });
            Assert.NotNull(ex);
            Assert.Contains("Invalid or unsupported color type", ex.Message);
        }

        // https://github.com/SixLabors/ImageSharp/issues/1014
        [Theory]
        [WithFileCollection(nameof(TestImagesIssue1014), PixelTypes.Rgba32)]
        public void Issue1014_DataSplitOverMultipleIDatChunks<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                        image.CompareToOriginal(provider, ImageComparer.Exact);
                    }
                });
            Assert.Null(ex);
        }

        // https://github.com/SixLabors/ImageSharp/issues/1177
        [Theory]
        [WithFileCollection(nameof(TestImagesIssue1177), PixelTypes.Rgba32)]
        public void Issue1177_CRC_Omitted<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                        image.CompareToOriginal(provider, ImageComparer.Exact);
                    }
                });
            Assert.Null(ex);
        }

        // https://github.com/SixLabors/ImageSharp/issues/1127
        [Theory]
        [WithFile(TestImages.Png.Issue1127, PixelTypes.Rgba32)]
        public void Issue1127<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);
                        image.CompareToOriginal(provider, ImageComparer.Exact);
                    }
                });
            Assert.Null(ex);
        }

        // https://github.com/SixLabors/ImageSharp/issues/1047
        [Theory]
        [WithFile(TestImages.Png.Bad.Issue1047_BadEndChunk, PixelTypes.Rgba32)]
        public void Issue1047<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);

                        // We don't have another x-plat reference decoder that can be compared for this image.
                        if (TestEnvironment.IsWindows)
                        {
                            image.CompareToOriginal(provider, ImageComparer.Exact, SystemDrawingReferenceDecoder.Instance);
                        }
                    }
                });
            Assert.Null(ex);
        }

        // https://github.com/SixLabors/ImageSharp/issues/410
        [Theory]
        [WithFile(TestImages.Png.Bad.Issue410_MalformedApplePng, PixelTypes.Rgba32)]
        public void Issue410_MalformedApplePng<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            System.Exception ex = Record.Exception(
                () =>
                {
                    using (Image<TPixel> image = provider.GetImage(PngDecoder))
                    {
                        image.DebugSave(provider);

                        // We don't have another x-plat reference decoder that can be compared for this image.
                        if (TestEnvironment.IsWindows)
                        {
                            image.CompareToOriginal(provider, ImageComparer.Exact, SystemDrawingReferenceDecoder.Instance);
                        }
                    }
                });
            Assert.NotNull(ex);
            Assert.Contains("Proprietary Apple PNG detected!", ex.Message);
        }

        [Theory]
        [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
        public void PngDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
            InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(PngDecoder));
            Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
        }

        [Theory]
        [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
        public void PngDecoder_CanDecode_WithLimitedAllocatorBufferCapacity(TestImageProvider<Rgba32> provider)
        {
            static void RunTest(string providerDump, string nonContiguousBuffersStr)
            {
                TestImageProvider<Rgba32> provider = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

                provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

                using Image<Rgba32> image = provider.GetImage(PngDecoder);
                image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);
                image.CompareToOriginal(provider);
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
