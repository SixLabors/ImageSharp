// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public class PngChunkTypeTests
    {
        [Fact]
        public void ChunkTypeIdsAreCorrect()
        {
            Assert.Equal(PngChunkType.Header, GetType("IHDR"));
            Assert.Equal(PngChunkType.Palette, GetType("PLTE"));
            Assert.Equal(PngChunkType.Data, GetType("IDAT"));
            Assert.Equal(PngChunkType.End, GetType("IEND"));
            Assert.Equal(PngChunkType.Transparency, GetType("tRNS"));
            Assert.Equal(PngChunkType.Text, GetType("tEXt"));
            Assert.Equal(PngChunkType.InternationalText, GetType("iTXt"));
            Assert.Equal(PngChunkType.CompressedText, GetType("zTXt"));
            Assert.Equal(PngChunkType.Chroma, GetType("cHRM"));
            Assert.Equal(PngChunkType.Gamma, GetType("gAMA"));
            Assert.Equal(PngChunkType.Physical, GetType("pHYs"));
            Assert.Equal(PngChunkType.Exif, GetType("eXIf"));
            Assert.Equal(PngChunkType.Time, GetType("tIME"));
            Assert.Equal(PngChunkType.Background, GetType("bKGD"));
            Assert.Equal(PngChunkType.EmbeddedColorProfile, GetType("iCCP"));
            Assert.Equal(PngChunkType.StandardRgbColourSpace, GetType("sRGB"));
            Assert.Equal(PngChunkType.SignificantBits, GetType("sBIT"));
            Assert.Equal(PngChunkType.Histogram, GetType("hIST"));
            Assert.Equal(PngChunkType.SuggestedPalette, GetType("sPLT"));
            Assert.Equal(PngChunkType.ProprietaryApple, GetType("CgBI"));
        }

        private static PngChunkType GetType(string text)
        {
            return (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(Encoding.ASCII.GetBytes(text));
        }
    }
}
