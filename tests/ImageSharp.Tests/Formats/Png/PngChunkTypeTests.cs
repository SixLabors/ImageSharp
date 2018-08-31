using System;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public class PngChunkTypeTests
    {
        [Fact]
        public void ChunkTypeIdsAreCorrect()
        {
            Assert.Equal(PngChunkType.Header,       GetType("IHDR"));
            Assert.Equal(PngChunkType.Palette,      GetType("PLTE"));
            Assert.Equal(PngChunkType.Data,         GetType("IDAT"));
            Assert.Equal(PngChunkType.End,          GetType("IEND"));
            Assert.Equal(PngChunkType.PaletteAlpha, GetType("tRNS"));
            Assert.Equal(PngChunkType.Text,         GetType("tEXt"));
            Assert.Equal(PngChunkType.Gamma,        GetType("gAMA"));
            Assert.Equal(PngChunkType.Physical,     GetType("pHYs"));
            Assert.Equal(PngChunkType.Exif,         GetType("eXIf"));
        }

        private static PngChunkType GetType(string text)
        {
            return (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(Encoding.UTF8.GetBytes(text));
        }
    }
}