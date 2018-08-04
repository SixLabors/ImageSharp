using System.Buffers.Binary;
using System.IO;
using System.Text;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public partial class PngDecoderTests
    {
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

                ImageFormatException exception =
                    Assert.Throws<ImageFormatException>(() => decoder.Decode<Rgb24>(null, memStream));

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
            memStream.Write(Raw1X1PngIhdrAndpHYs, 0, Raw1X1PngIhdrAndpHYs.Length);
        }

        private static void WriteChunk(MemoryStream memStream, string chunkName)
        {
            // Needs a minimum length of 9 for pHYs chunk.
            memStream.Write(new byte[] { 0, 0, 0, 9 }, 0, 4);
            memStream.Write(Encoding.GetEncoding("ASCII").GetBytes(chunkName), 0, 4); // 4 bytes chunk header
            memStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 9); // 9 bytes of chunk data
            memStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4); // Junk Crc
        }

        private static void WriteDataChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png data chunk containing a single black pixel
            memStream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
            memStream.Position = 0;
        }
    }
}