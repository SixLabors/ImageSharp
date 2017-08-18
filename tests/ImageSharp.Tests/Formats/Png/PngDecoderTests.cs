// <copyright file="PngDecoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.IO;
using System.IO.Compression;
using System.Text;
using ImageSharp.Formats;
using ImageSharp.PixelFormats;
using Xunit;

namespace ImageSharp.Tests
{
    public class PngDecoderTests
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32 | Tests.PixelTypes.RgbaVector | Tests.PixelTypes.Argb32;

        public static readonly string[] TestFiles =
            {
                TestImages.Png.Splash, TestImages.Png.Indexed, TestImages.Png.Interlaced, TestImages.Png.FilterVar,
                TestImages.Png.Bad.ChunkLength1, TestImages.Png.Bad.ChunkLength2, TestImages.Png.Rgb48Bpp, TestImages.Png.Rgb48BppInterlaced
            };

        [Theory]
        [WithFileCollection(nameof(TestFiles), PixelTypes)]
        public void DecodeAndReSave<TPixel>(TestImageProvider<TPixel> imageProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = imageProvider.GetImage())
            {
                imageProvider.Utility.SaveTestOutputFile(image, "bmp");
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
        public void DetectPixelSize(string imagePath, int expectedPixelSize)
        {
            TestFile testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.DetectPixelType(stream)?.BitsPerPixel);
            }
        }

        [Theory]
        [InlineData(PngChunkTypes.Header)]
        [InlineData(PngChunkTypes.Palette)]
        // [InlineData(PngChunkTypes.Data)] //TODO: Figure out how to test this
        [InlineData(PngChunkTypes.End)]
        public void Decode_IncorrectCRCForCriticalChunk_ExceptionIsThrown(string chunkName)
        {
            using (var memStream = new MemoryStream())
            {
                memStream.Skip(8);

                WriteChunk(memStream, chunkName);

                CompressStream(memStream);

                var decoder = new PngDecoder();

                ImageFormatException exception = Assert.Throws<ImageFormatException>(() =>
                {
                    decoder.Decode<Rgb24>(null, memStream);
                });

                Assert.Equal($"CRC Error. PNG {chunkName} chunk is corrupt!", exception.Message);
            }
        }

        [Theory]
        [InlineData(PngChunkTypes.Gamma)]
        [InlineData(PngChunkTypes.PaletteAlpha)]
        [InlineData(PngChunkTypes.Physical)]
        //[InlineData(PngChunkTypes.Text)] //TODO: Figure out how to test this
        public void Decode_IncorrectCRCForNonCriticalChunk_ExceptionIsThrown(string chunkName)
        {
            using (var memStream = new MemoryStream())
            {
                memStream.Skip(8);

                WriteChunk(memStream, chunkName);

                CompressStream(memStream);

                var decoder = new PngDecoder();
                decoder.Decode<Rgb24>(null, memStream);
            }
        }

        private static void WriteChunk(MemoryStream memStream, string chunkName)
        {
            memStream.Write(new byte[] { 0, 0, 0, 1 }, 0, 4);
            memStream.Write(Encoding.GetEncoding("ASCII").GetBytes(chunkName), 0, 4);
            memStream.Write(new byte[] { 0, 0, 0, 0, 0 }, 0, 5);
        }

        private static void CompressStream(Stream stream)
        {
            stream.Position = 0;
            using (var deflateStream = new DeflateStream(stream, CompressionLevel.NoCompression, true))
            {
            }
            stream.Position = 0;
        }
    }
}