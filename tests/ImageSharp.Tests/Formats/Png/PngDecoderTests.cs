// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.Buffers.Binary;
using System.IO;
using System.Text;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public class PngDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;
        
        // Contains the png marker, IHDR and pHYs chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] raw1x1PngIHDRAndpHYs =
         {
            // PNG Identifier 
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,

            // IHDR
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00,
            // IHDR CRC
            0x90, 0x77, 0x53, 0xDE,

            // pHYS
            0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01,
            // pHYS CRC
            0xC7, 0x6F, 0xA8, 0x64
        };

        // Contains the png marker, IDAT and IEND chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] raw1x1PngIDATAndIEND =
          {
            // IDAT
            0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x18, 0x57, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00,
            0x00, 0x04, 0x00, 0x01,

            // IDAT CRC
            0x5C, 0xCD, 0xFF, 0x69,

            // IEND
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
            0x4E, 0x44,

            // IEND CRC
            0xAE, 0x42, 0x60, 0x82
        };

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
            TestImages.Png.GrayAlpha8Bit
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

        public static readonly string[] TestImagesGray16Bit =
        {
            TestImages.Png.Gray16Bit,
        };

        public static readonly string[] TestImagesGrayAlpha16Bit =
        {
            TestImages.Png.GrayAlpha16Bit,
            TestImages.Png.GrayTrns16BitInterlaced
        };

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
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
        [WithFileCollection(nameof(TestImagesGray16Bit), PixelTypes.Rgb48)]
        public void Decode_Gray16Bit<TPixel>(TestImageProvider<TPixel> provider)
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

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_TextChunckIsRead()
        {
            var options = new PngDecoder()
            {
                IgnoreMetadata = false
            };

            var testFile = TestFile.Create(TestImages.Png.Blur);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("Software", image.MetaData.Properties[0].Name);
                Assert.Equal("paint.net 4.0.6", image.MetaData.Properties[0].Value);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_TextChunksAreIgnored()
        {
            var options = new PngDecoder()
            {
                IgnoreMetadata = true
            };

            var testFile = TestFile.Create(TestImages.Png.Blur);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(0, image.MetaData.Properties.Count);
            }
        }

        [Fact]
        public void Decode_TextEncodingSetToUnicode_TextIsReadWithCorrectEncoding()
        {
            var options = new PngDecoder()
            {
                TextEncoding = Encoding.Unicode
            };

            var testFile = TestFile.Create(TestImages.Png.Blur);

            using (Image<Rgba32> image = testFile.CreateImage(options))
            {
                Assert.Equal(1, image.MetaData.Properties.Count);
                Assert.Equal("潓瑦慷敲", image.MetaData.Properties[0].Name);
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
        [InlineData((uint)PngChunkType.Header)] // IHDR
        [InlineData((uint)PngChunkType.Palette)] // PLTE
        // [InlineData(PngChunkTypes.Data)] //TODO: Figure out how to test this
        [InlineData((uint)PngChunkType.End)] // IEND
        public void Decode_IncorrectCRCForCriticalChunk_ExceptionIsThrown(uint chunkType)
        {
            string chunkName = GetChunkTypeName(chunkType);

            using (var memStream = new MemoryStream())
            {
                WriteHeaderChunk(memStream);
                WriteChunk(memStream, chunkName);
                WriteDataChunk(memStream);

                var decoder = new PngDecoder();

                ImageFormatException exception = Assert.Throws<ImageFormatException>(() => decoder.Decode<Rgb24>(null, memStream));

                Assert.Equal($"CRC Error. PNG {chunkName} chunk is corrupt!", exception.Message);
            }
        }

        [Theory]
        [InlineData((uint)PngChunkType.Gamma)] // gAMA
        [InlineData((uint)PngChunkType.PaletteAlpha)] // tRNS
        [InlineData((uint)PngChunkType.Physical)] // pHYs: It's ok to test physical as we don't throw for duplicate chunks.
        //[InlineData(PngChunkTypes.Text)] //TODO: Figure out how to test this
        public void Decode_IncorrectCRCForNonCriticalChunk_ExceptionIsThrown(uint chunkType)
        {
            string chunkName = GetChunkTypeName(chunkType);

            using (var memStream = new MemoryStream())
            {
                WriteHeaderChunk(memStream);
                WriteChunk(memStream, chunkName);
                WriteDataChunk(memStream);

                var decoder = new PngDecoder();
                decoder.Decode<Rgb24>(null, memStream);
            }
        }

        private static string GetChunkTypeName(uint value)
        {
            byte[] data = new byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(data, value);

            return Encoding.ASCII.GetString(data);
        }

        private static void WriteHeaderChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png header chunk containing a single black pixel
            memStream.Write(raw1x1PngIHDRAndpHYs, 0, raw1x1PngIHDRAndpHYs.Length);
        }

        private static void WriteChunk(MemoryStream memStream, string chunkName)
        {
            memStream.Write(new byte[] { 0, 0, 0, 1 }, 0, 4);
            memStream.Write(Encoding.GetEncoding("ASCII").GetBytes(chunkName), 0, 4);
            memStream.Write(new byte[] { 0, 0, 0, 0, 0 }, 0, 5);
        }

        private static void WriteDataChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png data chunk containing a single black pixel
            memStream.Write(raw1x1PngIDATAndIEND, 0, raw1x1PngIDATAndIEND.Length);
            memStream.Position = 0;
        }
    }
}