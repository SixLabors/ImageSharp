// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.IO.Compression;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
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