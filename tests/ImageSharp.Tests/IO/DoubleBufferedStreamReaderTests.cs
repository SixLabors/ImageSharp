// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.IO;
using SixLabors.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    public class DoubleBufferedStreamReaderTests
    {
        private readonly MemoryAllocator allocator = Configuration.Default.MemoryAllocator;

        [Fact]
        public void DoubleBufferedStreamReaderCanReadSingleByteFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);

                Assert.Equal(expected[0], reader.ReadByte());

                // We've read a whole chunk but increment by 1 in our reader.
                Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength);
                Assert.Equal(1, reader.Position);
            }
        }

        [Fact]
        public void DoubleBufferedStreamReaderCanReadSingleByteFromOffset()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                const int offset = 5;
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);
                reader.Position = offset;

                Assert.Equal(expected[offset], reader.ReadByte());

                // We've read a whole chunk but increment by 1 in our reader.
                Assert.Equal(stream.Position, DoubleBufferedStreamReader.ChunkLength + offset);
                Assert.Equal(offset + 1, reader.Position);
            }
        }

        [Fact]
        public void DoubleBufferedStreamReaderCanReadSubsequentSingleByteCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);

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
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] buffer = new byte[2];
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);

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
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] buffer = new byte[2];
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);

                for (int i = 0, o = 0; i < expected.Length / 2; i++, o += 2)
                {

                    Assert.Equal(2, reader.Read(buffer, 0, 2));
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

        [Fact]
        public void DoubleBufferedStreamReaderCanSkip()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                var reader = new DoubleBufferedStreamReader(this.allocator, stream);

                int skip = 50;
                int plusOne = 1;
                int skip2 = DoubleBufferedStreamReader.ChunkLength;

                // Skip
                reader.Skip(skip);
                Assert.Equal(skip, reader.Position);
                Assert.Equal(stream.Position, reader.Position);

                // Read
                Assert.Equal(expected[skip], reader.ReadByte());

                // Skip Again
                reader.Skip(skip2);

                // First Skip + First Read + Second Skip
                int position = skip + plusOne + skip2;

                Assert.Equal(position, reader.Position);
                Assert.Equal(stream.Position, reader.Position);
                Assert.Equal(expected[position], reader.ReadByte());
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
