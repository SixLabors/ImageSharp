// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    public class BufferedReadStreamTests
    {
        private readonly Configuration configuration;
        private readonly int bufferSize;

        public BufferedReadStreamTests()
        {
            this.configuration = Configuration.Default;
            this.bufferSize = this.configuration.StreamProcessingBufferSize;
        }

        [Fact]
        public void BufferedStreamCanReadSingleByteFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    Assert.Equal(expected[0], reader.ReadByte());

                    // We've read a whole chunk but increment by 1 in our reader.
                    Assert.Equal(this.bufferSize, stream.Position);
                    Assert.Equal(1, reader.Position);
                }

                // Position of the stream should be reset on disposal.
                Assert.Equal(1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadSingleByteFromOffset()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                byte[] expected = stream.ToArray();
                const int offset = 5;
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    reader.Position = offset;

                    Assert.Equal(expected[offset], reader.ReadByte());

                    // We've read a whole chunk but increment by 1 in our reader.
                    Assert.Equal(this.bufferSize + offset, stream.Position);
                    Assert.Equal(offset + 1, reader.Position);
                }

                Assert.Equal(offset + 1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadSubsequentSingleByteCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                byte[] expected = stream.ToArray();
                int i;
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    for (i = 0; i < expected.Length; i++)
                    {
                        Assert.Equal(expected[i], reader.ReadByte());
                        Assert.Equal(i + 1, reader.Position);

                        if (i < this.bufferSize)
                        {
                            Assert.Equal(stream.Position, this.bufferSize);
                        }
                        else if (i >= this.bufferSize && i < this.bufferSize * 2)
                        {
                            // We should have advanced to the second chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 2);
                        }
                        else
                        {
                            // We should have advanced to the third chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 3);
                        }
                    }
                }

                Assert.Equal(i, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamCanReadMultipleBytesFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                var buffer = new byte[2];
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    Assert.Equal(2, reader.Read(buffer, 0, 2));
                    Assert.Equal(expected[0], buffer[0]);
                    Assert.Equal(expected[1], buffer[1]);

                    // We've read a whole chunk but increment by the buffer length in our reader.
                    Assert.Equal(stream.Position, this.bufferSize);
                    Assert.Equal(buffer.Length, reader.Position);
                }
            }
        }

        [Fact]
        public void BufferedStreamCanReadSubsequentMultipleByteCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                var buffer = new byte[2];
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    for (int i = 0, o = 0; i < expected.Length / 2; i++, o += 2)
                    {
                        Assert.Equal(2, reader.Read(buffer, 0, 2));
                        Assert.Equal(expected[o], buffer[0]);
                        Assert.Equal(expected[o + 1], buffer[1]);
                        Assert.Equal(o + 2, reader.Position);

                        int offset = i * 2;
                        if (offset < this.bufferSize)
                        {
                            Assert.Equal(stream.Position, this.bufferSize);
                        }
                        else if (offset >= this.bufferSize && offset < this.bufferSize * 2)
                        {
                            // We should have advanced to the second chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 2);
                        }
                        else
                        {
                            // We should have advanced to the third chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 3);
                        }
                    }
                }
            }
        }

        [Fact]
        public void BufferedStreamCanReadSubsequentMultipleByteSpanCorrectly()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                Span<byte> buffer = new byte[2];
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    for (int i = 0, o = 0; i < expected.Length / 2; i++, o += 2)
                    {
                        Assert.Equal(2, reader.Read(buffer, 0, 2));
                        Assert.Equal(expected[o], buffer[0]);
                        Assert.Equal(expected[o + 1], buffer[1]);
                        Assert.Equal(o + 2, reader.Position);

                        int offset = i * 2;
                        if (offset < this.bufferSize)
                        {
                            Assert.Equal(stream.Position, this.bufferSize);
                        }
                        else if (offset >= this.bufferSize && offset < this.bufferSize * 2)
                        {
                            // We should have advanced to the second chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 2);
                        }
                        else
                        {
                            // We should have advanced to the third chunk now.
                            Assert.Equal(stream.Position, this.bufferSize * 3);
                        }
                    }
                }
            }
        }

        [Fact]
        public void BufferedStreamCanSkip()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    int skip = 50;
                    int plusOne = 1;
                    int skip2 = this.bufferSize;

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
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize / 4))
            {
                byte[] expected = stream.ToArray();
                const int offset = 5;
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    reader.Position = offset;

                    Assert.Equal(expected[offset], reader.ReadByte());

                    // We've read a whole length of the stream but increment by 1 in our reader.
                    Assert.Equal(this.bufferSize / 4, stream.Position);
                    Assert.Equal(offset + 1, reader.Position);
                }

                Assert.Equal(offset + 1, stream.Position);
            }
        }

        [Fact]
        public void BufferedStreamReadsCanReadAllAsSingleByteFromOrigin()
        {
            using (MemoryStream stream = this.CreateTestStream(this.bufferSize * 3))
            {
                byte[] expected = stream.ToArray();
                using (var reader = new BufferedReadStream(this.configuration, stream))
                {
                    for (int i = 0; i < expected.Length; i++)
                    {
                        Assert.Equal(expected[i], reader.ReadByte());
                    }
                }
            }
        }

        private MemoryStream CreateTestStream(int length)
        {
            var buffer = new byte[length];
            var random = new Random();
            random.NextBytes(buffer);

            return new EvilStream(buffer);
        }

        // Simulates a stream that can only return 1 byte at a time per read instruction.
        // See https://github.com/SixLabors/ImageSharp/issues/1268
        private class EvilStream : MemoryStream
        {
            public EvilStream(byte[] buffer)
                : base(buffer)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, 1);
            }
        }
    }
}
