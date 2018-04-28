// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class DoubleBufferedStreamReaderTests
    {
        [Fact]
        public void DoubleBufferedStreamReaderCanReadSingleByteFromOrigin()
        {
            using (MemoryStream stream = CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(stream);

                Assert.Equal(expected[0], reader.ReadByte());

                // We've read a whole chunk but increment by 1 in our reader.
                Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength);
                Assert.Equal(1, reader.Position);
            }
        }

        [Fact]
        public void DoubleBufferedStreamReaderCanReadSubsequentSingleByteCorrectly()
        {
            using (MemoryStream stream = CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(stream);

                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i], reader.ReadByte());
                    Assert.Equal(i + 1, reader.Position);

                    if (i < DoubleBufferedStreamReader.ChunkLength)
                    {
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength);
                    }
                    else if (i >= DoubleBufferedStreamReader.ChunkLength && i < DoubleBufferedStreamReader.ChunkLength * 2)
                    {
                        // We should have advanced to the second chunk now.
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength * 2);
                    }
                    else
                    {
                        // We should have advanced to the third chunk now.
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength * 3);
                    }
                }
            }
        }

        [Fact]
        public void DoubleBufferedStreamReaderCanReadMultipleBytesFromOrigin()
        {
            using (MemoryStream stream = CreateTestStream())
            {
                byte[] buffer = new byte[2];
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(stream);

                Assert.Equal(2, reader.Read(buffer, 0, 2));
                Assert.Equal(expected[0], buffer[0]);
                Assert.Equal(expected[1], buffer[1]);

                // We've read a whole chunk but increment by the buffer length in our reader.
                Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength);
                Assert.Equal(buffer.Length, reader.Position);
            }
        }

        [Fact]
        public void DoubleBufferedStreamReaderCanReadSubsequentMultipleByteCorrectly()
        {
            using (MemoryStream stream = CreateTestStream())
            {
                byte[] buffer = new byte[2];
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(stream);

                for (int i = 0, o = 0; i < expected.Length / 2; i++, o += 2)
                {
                    if (o + 2 == expected.Length)
                    {
                        // We've reached the end of the stream
                        Assert.Equal(0, reader.Read(buffer, 0, 2));
                    }
                    else
                    {
                        Assert.Equal(2, reader.Read(buffer, 0, 2));
                    }

                    Assert.Equal(expected[o], buffer[0]);
                    Assert.Equal(expected[o + 1], buffer[1]);
                    Assert.Equal(o + 2, reader.Position);

                    int offset = i * 2;
                    if (offset < DoubleBufferedStreamReader.ChunkLength)
                    {
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength);
                    }
                    else if (offset >= DoubleBufferedStreamReader.ChunkLength && offset < DoubleBufferedStreamReader.ChunkLength * 2)
                    {
                        // We should have advanced to the second chunk now.
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength * 2);
                    }
                    else
                    {
                        // We should have advanced to the third chunk now.
                        Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength * 3);
                    }
                }
            }
        }

        private MemoryStream CreateTestStream()
        {
            byte[] buffer = new byte[DoubleBufferedStreamReader.ChunkLength * 3];
            var random = new Random();
            random.NextBytes(buffer);

            return new MemoryStream(buffer);
        }
    }
}
