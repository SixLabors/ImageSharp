// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    public class BufferedReadStreamTests
    {
        [Fact]
        public void BufferedStreamCanReadSingleByteFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(stream))
                {
                    Assert.Equal(expected[0], reader.ReadByte());

                    // We've read a whole chunk but increment by 1 in our reader.
                    Assert.Equal(BufferedReadStream.BufferLength, stream.Position);
                    Assert.Equal(1, reader.Position);
                }

                // Position of the stream should be reset on disposal.
                Assert.Equal(1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadSingleByteFromOffset()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                const int offset = 5;
                using (var reader = new BufferedReadStream(stream))
                {
                    reader.Position = offset;

                    Assert.Equal(expected[offset], reader.ReadByte());

                    // We've read a whole chunk but increment by 1 in our reader.
                    Assert.Equal(BufferedReadStream.BufferLength + offset, stream.Position);
                    Assert.Equal(offset + 1, reader.Position);
                }

                Assert.Equal(offset + 1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadSubsequentSingleByteCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                int i;
                using (var reader = new BufferedReadStream(stream))
                {
                    for (i = 0; i < expected.Length; i++)
                    {
                        Assert.Equal(expected[i], reader.ReadByte());
                        Assert.Equal(i + 1, reader.Position);

                        if (i < BufferedReadStream.BufferLength)
                        {
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength);
                        }
                        else if (i >= BufferedReadStream.BufferLength && i < BufferedReadStream.BufferLength * 2)
                        {
                            // We should have advanced to the second chunk now.
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength * 2);
                        }
                        else
                        {
                            // We should have advanced to the third chunk now.
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength * 3);
                        }
                    }
                }

                Assert.Equal(i, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadMultipleBytesFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                var buffer = new byte[2];
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(stream))
                {
                    Assert.Equal(2, reader.Read(buffer, 0, 2));
                    Assert.Equal(expected[0], buffer[0]);
                    Assert.Equal(expected[1], buffer[1]);

                    // We've read a whole chunk but increment by the buffer length in our reader.
                    Assert.Equal(stream.Position, BufferedReadStream.BufferLength);
                    Assert.Equal(buffer.Length, reader.Position);
                }
            }
        }

        [Fact]
        public void BufferedStreamCanReadSubsequentMultipleByteCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                var buffer = new byte[2];
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(stream))
                {
                    for (int i = 0, o = 0; i < expected.Length / 2; i++, o += 2)
                    {
                        Assert.Equal(2, reader.Read(buffer, 0, 2));
                        Assert.Equal(expected[o], buffer[0]);
                        Assert.Equal(expected[o + 1], buffer[1]);
                        Assert.Equal(o + 2, reader.Position);

                        int offset = i * 2;
                        if (offset < BufferedReadStream.BufferLength)
                        {
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength);
                        }
                        else if (offset >= BufferedReadStream.BufferLength && offset < BufferedReadStream.BufferLength * 2)
                        {
                            // We should have advanced to the second chunk now.
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength * 2);
                        }
                        else
                        {
                            // We should have advanced to the third chunk now.
                            Assert.Equal(stream.Position, BufferedReadStream.BufferLength * 3);
                        }
                    }
                }
            }
        }

        [Fact]
        public void BufferedStreamCanSkip()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(stream))
                {
                    int skip = 50;
                    int plusOne = 1;
                    int skip2 = BufferedReadStream.BufferLength;

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
        }

        [Fact]
        public void BufferedStreamReadsSmallStream()
        {
            // Create a stream smaller than the default buffer length
            using (MemoryStream stream = this.CreateTestStream(BufferedReadStream.BufferLength / 4))
            {
                byte[] expected = stream.ToArray();
                const int offset = 5;
                using (var reader = new BufferedReadStream(stream))
                {
                    reader.Position = offset;

                    Assert.Equal(expected[offset], reader.ReadByte());

                    // We've read a whole length of the stream but increment by 1 in our reader.
                    Assert.Equal(BufferedReadStream.BufferLength / 4, stream.Position);
                    Assert.Equal(offset + 1, reader.Position);
                }

                Assert.Equal(offset + 1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamReadsCanReadAllAsSingleByteFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream())
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(stream))
                {
                    for (int i = 0; i < expected.Length; i++)
                    {
                        Assert.Equal(expected[i], reader.ReadByte());
                    }
                }
            }
        }

        private MemoryStream CreateTestStream(int length = BufferedReadStream.BufferLength * 3)
        {
            var buffer = new byte[length];
            var random = new Random();
            random.NextBytes(buffer);

            return new MemoryStream(buffer);
        }
    }
}
