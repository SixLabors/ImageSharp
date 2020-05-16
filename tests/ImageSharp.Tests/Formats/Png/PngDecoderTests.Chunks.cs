// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public partial class PngDecoderTests
    {
        // Represents ASCII string of "123456789"
        private readonly byte[] check = { 49, 50, 51, 52, 53, 54, 55, 56, 57 };

        // Contains the png marker, IHDR and pHYs chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] Raw1X1PngIhdrAndpHYs =
            {
                // PNG Identifier
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,

                // IHDR
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02,
                0x00, 0x00, 0x00,

                // IHDR CRC
                0x90, 0x77, 0x53, 0xDE,

                // pHYS
                0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00,
                0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01,

                // pHYS CRC
                0xC7, 0x6F, 0xA8, 0x64
            };

        // Contains the png marker, IDAT and IEND chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] Raw1X1PngIdatAndIend =
            {
                // IDAT
                0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x18,
                0x57, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04,
                0x00, 0x01,

                // IDAT CRC
                0x5C, 0xCD, 0xFF, 0x69,

                // IEND
                0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,

                // IEND CRC
                0xAE, 0x42, 0x60, 0x82
            };

        [Theory]
        [InlineData((uint)PngChunkType.Header)] // IHDR
        [InlineData((uint)PngChunkType.Palette)] // PLTE
        /* [InlineData(PngChunkTypes.Data)] TODO: Figure out how to test this */
        public void Decode_IncorrectCRCForCriticalChunk_ExceptionIsThrown(uint chunkType)
        {
            string chunkName = GetChunkTypeName(chunkType);

            using (var memStream = new MemoryStream())
            {
                WriteHeaderChunk(memStream);
                WriteChunk(memStream, chunkName);
                WriteDataChunk(memStream);

                var decoder = new PngDecoder();

                ImageFormatException exception =
                    Assert.Throws<InvalidImageContentException>(() => decoder.Decode<Rgb24>(null, memStream));

                Assert.Equal($"CRC Error. PNG {chunkName} chunk is corrupt!", exception.Message);
            }
        }

        [Fact]
        public void CalculateCrc_Works_LongerRun()
        {
            // Longer run, enough to require moving the point in SIMD implementation with
            // offset for dropping back to scalar.
            var data = new byte[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8,
                9, 10, 11, 12, 13, 14, 15, 16,
                17, 18, 19, 20, 21, 22, 23, 24,
                25, 26, 27, 28, 29, 30, 31, 32,
                33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48,
                49, 50, 51, 52, 53, 54, 55, 56,
                57, 58, 59, 60, 61, 62, 63, 64,
                65, 66, 67, 68, 69, 70, 71, 72,
                73, 74, 75, 76, 77, 78, 79, 80,
                81, 82, 83, 84, 85, 86, 87, 88,
                89, 90, 91, 92, 93, 94, 95, 96,
                97, 98, 99, 100, 101, 102, 103, 104,
                105, 106, 107, 108, 109, 110, 111, 112,
                113, 114, 115, 116, 117, 118, 119, 120,
                121, 122, 123, 124, 125, 126, 127, 128,
                129, 130, 131, 132, 133, 134, 135, 136,
                137, 138, 139, 140, 141, 142, 143, 144,
                145, 146, 147, 148, 149, 150, 151, 152,
                153, 154, 155, 156, 157, 158, 159, 160,
                161, 162, 163, 164, 165, 166, 167, 168,
                169, 170, 171, 172, 173, 174, 175, 176,
                177, 178, 179, 180, 181, 182, 183, 184,
                185, 186, 187, 188, 189, 190, 191, 192,
                193, 194, 195, 196, 197, 198, 199, 200,
                201, 202, 203, 204, 205, 206, 207, 208,
                209, 210, 211, 212, 213, 214, 215
            };

            // assert
            uint crc = Crc32.Calculate(data);
            Assert.Equal(0xC1125402, crc);
        }

        [Fact]
        public void CalculateCrc_Works()
        {
            // Short run, less than 64.
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            uint crc = Crc32.Calculate(data);

            Assert.Equal(0x88AA689F, crc);
        }

        private static string GetChunkTypeName(uint value)
        {
            var data = new byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(data, value);

            return Encoding.ASCII.GetString(data);
        }

        private static void WriteHeaderChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png header chunk containing a single black pixel.
            memStream.Write(Raw1X1PngIhdrAndpHYs, 0, Raw1X1PngIhdrAndpHYs.Length);
        }

        private static void WriteChunk(MemoryStream memStream, string chunkName)
        {
            // Needs a minimum length of 9 for pHYs chunk.
            memStream.Write(new byte[] { 0, 0, 0, 9 }, 0, 4);
            memStream.Write(Encoding.ASCII.GetBytes(chunkName), 0, 4); // 4 bytes chunk header
            memStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 9); // 9 bytes of chunk data
            memStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4); // Junk Crc
        }

        private static void WriteDataChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png data chunk containing a single black pixel.
            memStream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
            memStream.Position = 0;
        }
    }
}
