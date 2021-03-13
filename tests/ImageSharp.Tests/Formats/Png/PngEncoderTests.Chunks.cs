// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public partial class PngEncoderTests
    {
        [Fact]
        public void HeaderChunk_ComesFirst()
        {
            // arrange
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, PngEncoder);

            // assert
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
            var type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            Assert.Equal(PngChunkType.Header, type);
        }

        [Fact]
        public void EndChunk_IsLast()
        {
            // arrange
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, PngEncoder);

            // assert
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            bool endChunkFound = false;
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                Assert.False(endChunkFound);
                if (type == PngChunkType.End)
                {
                    endChunkFound = true;
                }

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }
        }

        [Theory]
        [InlineData(PngChunkType.Gamma)]
        [InlineData(PngChunkType.Chroma)]
        [InlineData(PngChunkType.EmbeddedColorProfile)]
        [InlineData(PngChunkType.SignificantBits)]
        [InlineData(PngChunkType.StandardRgbColourSpace)]
        public void Chunk_ComesBeforePlteAndIDat(object chunkTypeObj)
        {
            // arrange
            var chunkType = (PngChunkType)chunkTypeObj;
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, PngEncoder);

            // assert
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            bool palFound = false;
            bool dataFound = false;
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                if (chunkType == type)
                {
                    Assert.False(palFound || dataFound, $"{chunkType} chunk should come before data and palette chunk");
                }

                switch (type)
                {
                    case PngChunkType.Data:
                        dataFound = true;
                        break;
                    case PngChunkType.Palette:
                        palFound = true;
                        break;
                }

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }
        }

        [Theory]
        [InlineData(PngChunkType.Physical)]
        [InlineData(PngChunkType.SuggestedPalette)]
        public void Chunk_ComesBeforeIDat(object chunkTypeObj)
        {
            // arrange
            var chunkType = (PngChunkType)chunkTypeObj;
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, PngEncoder);

            // assert
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            bool dataFound = false;
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                if (chunkType == type)
                {
                    Assert.False(dataFound, $"{chunkType} chunk should come before data chunk");
                }

                if (type == PngChunkType.Data)
                {
                    dataFound = true;
                }

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }
        }

        [Fact]
        public void IgnoreMetadata_WillExcludeAllAncillaryChunks()
        {
            // arrange
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();
            var encoder = new PngEncoder() { IgnoreMetadata = true, TextCompressionThreshold = 8 };
            var expectedChunkTypes = new Dictionary<PngChunkType, bool>()
            {
                { PngChunkType.Header, false },
                { PngChunkType.Palette, false },
                { PngChunkType.Data, false },
                { PngChunkType.End, false }
            };
            var excludedChunkTypes = new List<PngChunkType>()
            {
                PngChunkType.Gamma,
                PngChunkType.Exif,
                PngChunkType.Physical,
                PngChunkType.Text,
                PngChunkType.InternationalText,
                PngChunkType.CompressedText,
            };

            // act
            input.Save(memStream, encoder);

            // assert
            Assert.True(excludedChunkTypes.Count > 0);
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                Assert.False(excludedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been excluded");
                if (expectedChunkTypes.ContainsKey(chunkType))
                {
                    expectedChunkTypes[chunkType] = true;
                }

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }

            // all expected chunk types should have been seen at least once.
            foreach (PngChunkType chunkType in expectedChunkTypes.Keys)
            {
                Assert.True(expectedChunkTypes[chunkType], $"We expect {chunkType} chunk to be present at least once");
            }
        }

        [Theory]
        [InlineData(PngChunkFilter.ExcludeGammaChunk)]
        [InlineData(PngChunkFilter.ExcludeExifChunk)]
        [InlineData(PngChunkFilter.ExcludePhysicalChunk)]
        [InlineData(PngChunkFilter.ExcludeTextChunks)]
        [InlineData(PngChunkFilter.ExcludeAll)]
        public void ExcludeFilter_Works(object filterObj)
        {
            // arrange
            var chunkFilter = (PngChunkFilter)filterObj;
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();
            var encoder = new PngEncoder() { ChunkFilter = chunkFilter, TextCompressionThreshold = 8 };
            var expectedChunkTypes = new Dictionary<PngChunkType, bool>()
            {
                { PngChunkType.Header, false },
                { PngChunkType.Gamma, false },
                { PngChunkType.Palette, false },
                { PngChunkType.InternationalText, false },
                { PngChunkType.Text, false },
                { PngChunkType.CompressedText, false },
                { PngChunkType.Exif, false },
                { PngChunkType.Physical, false },
                { PngChunkType.Data, false },
                { PngChunkType.End, false }
            };
            var excludedChunkTypes = new List<PngChunkType>();
            switch (chunkFilter)
            {
                case PngChunkFilter.ExcludeGammaChunk:
                    excludedChunkTypes.Add(PngChunkType.Gamma);
                    expectedChunkTypes.Remove(PngChunkType.Gamma);
                    break;
                case PngChunkFilter.ExcludeExifChunk:
                    excludedChunkTypes.Add(PngChunkType.Exif);
                    expectedChunkTypes.Remove(PngChunkType.Exif);
                    break;
                case PngChunkFilter.ExcludePhysicalChunk:
                    excludedChunkTypes.Add(PngChunkType.Physical);
                    expectedChunkTypes.Remove(PngChunkType.Physical);
                    break;
                case PngChunkFilter.ExcludeTextChunks:
                    excludedChunkTypes.Add(PngChunkType.Text);
                    excludedChunkTypes.Add(PngChunkType.InternationalText);
                    excludedChunkTypes.Add(PngChunkType.CompressedText);
                    expectedChunkTypes.Remove(PngChunkType.Text);
                    expectedChunkTypes.Remove(PngChunkType.InternationalText);
                    expectedChunkTypes.Remove(PngChunkType.CompressedText);
                    break;
                case PngChunkFilter.ExcludeAll:
                    excludedChunkTypes.Add(PngChunkType.Gamma);
                    excludedChunkTypes.Add(PngChunkType.Exif);
                    excludedChunkTypes.Add(PngChunkType.Physical);
                    excludedChunkTypes.Add(PngChunkType.Text);
                    excludedChunkTypes.Add(PngChunkType.InternationalText);
                    excludedChunkTypes.Add(PngChunkType.CompressedText);
                    expectedChunkTypes.Remove(PngChunkType.Gamma);
                    expectedChunkTypes.Remove(PngChunkType.Exif);
                    expectedChunkTypes.Remove(PngChunkType.Physical);
                    expectedChunkTypes.Remove(PngChunkType.Text);
                    expectedChunkTypes.Remove(PngChunkType.InternationalText);
                    expectedChunkTypes.Remove(PngChunkType.CompressedText);
                    break;
            }

            // act
            input.Save(memStream, encoder);

            // assert
            Assert.True(excludedChunkTypes.Count > 0);
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                Assert.False(excludedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been excluded");
                if (expectedChunkTypes.ContainsKey(chunkType))
                {
                    expectedChunkTypes[chunkType] = true;
                }

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }

            // all expected chunk types should have been seen at least once.
            foreach (PngChunkType chunkType in expectedChunkTypes.Keys)
            {
                Assert.True(expectedChunkTypes[chunkType], $"We expect {chunkType} chunk to be present at least once");
            }
        }

        [Fact]
        public void ExcludeFilter_WithNone_DoesNotExcludeChunks()
        {
            // arrange
            var testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();
            var encoder = new PngEncoder() { ChunkFilter = PngChunkFilter.None, TextCompressionThreshold = 8 };
            var expectedChunkTypes = new List<PngChunkType>()
            {
                PngChunkType.Header,
                PngChunkType.Gamma,
                PngChunkType.Palette,
                PngChunkType.InternationalText,
                PngChunkType.Text,
                PngChunkType.CompressedText,
                PngChunkType.Exif,
                PngChunkType.Physical,
                PngChunkType.Data,
                PngChunkType.End,
            };

            // act
            input.Save(memStream, encoder);
            memStream.Position = 0;
            Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
            while (bytesSpan.Length > 0)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(0, 4));
                var chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
                Assert.True(expectedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been present");

                bytesSpan = bytesSpan.Slice(4 + 4 + length + 4);
            }
        }
    }
}
