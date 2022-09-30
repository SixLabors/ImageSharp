// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.IO;

public class BufferedReadStreamTests
{
    private readonly Configuration configuration;

    public BufferedReadStreamTests()
        => this.configuration = Configuration.CreateDefaultInstance();

    public static readonly TheoryData<int> BufferSizes =
        new()
        {
            1, 2, 4, 8,
            16, 97, 503,
            719, 1024,
            8096, 64768
        };

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadSingleByteFromOrigin(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        byte[] expected = stream.ToArray();
        using (BufferedReadStream reader = new(this.configuration, stream))
        {
            Assert.Equal(expected[0], reader.ReadByte());

            // We've read a whole chunk but increment by 1 in our reader.
            Assert.True(stream.Position >= bufferSize);
            Assert.Equal(1, reader.Position);
        }

        // Position of the stream should be reset on disposal.
        Assert.Equal(1, stream.Position);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadSingleByteFromOffset(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        byte[] expected = stream.ToArray();
        int offset = expected.Length / 2;
        using (BufferedReadStream reader = new(this.configuration, stream))
        {
            reader.Position = offset;

            Assert.Equal(expected[offset], reader.ReadByte());

            // We've read a whole chunk but increment by 1 in our reader.
            Assert.Equal(bufferSize + offset, stream.Position);
            Assert.Equal(offset + 1, reader.Position);
        }

        Assert.Equal(offset + 1, stream.Position);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadSubsequentSingleByteCorrectly(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        byte[] expected = stream.ToArray();
        int i;
        using (BufferedReadStream reader = new(this.configuration, stream))
        {
            for (i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], reader.ReadByte());
                Assert.Equal(i + 1, reader.Position);

                if (i < bufferSize)
                {
                    Assert.Equal(stream.Position, bufferSize);
                }
                else if (i >= bufferSize && i < bufferSize * 2)
                {
                    // We should have advanced to the second chunk now.
                    Assert.Equal(stream.Position, bufferSize * 2);
                }
                else
                {
                    // We should have advanced to the third chunk now.
                    Assert.Equal(stream.Position, bufferSize * 3);
                }
            }
        }

        Assert.Equal(i, stream.Position);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadMultipleBytesFromOrigin(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        byte[] buffer = new byte[2];
        byte[] expected = stream.ToArray();
        using BufferedReadStream reader = new(this.configuration, stream);
        Assert.Equal(2, reader.Read(buffer, 0, 2));
        Assert.Equal(expected[0], buffer[0]);
        Assert.Equal(expected[1], buffer[1]);

        // We've read a whole chunk but increment by the buffer length in our reader.
        Assert.True(stream.Position >= bufferSize);
        Assert.Equal(buffer.Length, reader.Position);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadSubsequentMultipleByteCorrectly(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        const int increment = 2;
        byte[] buffer = new byte[2];
        byte[] expected = stream.ToArray();
        using BufferedReadStream reader = new(this.configuration, stream);
        for (int i = 0, o = 0; i < expected.Length / increment; i++, o += increment)
        {
            // Check values are correct.
            Assert.Equal(increment, reader.Read(buffer, 0, increment));
            Assert.Equal(expected[o], buffer[0]);
            Assert.Equal(expected[o + 1], buffer[1]);
            Assert.Equal(o + increment, reader.Position);

            // These tests ensure that we are correctly reading
            // our buffer in chunks of the given size.
            int offset = i * increment;

            // First chunk.
            if (offset < bufferSize)
            {
                // We've read an entire chunk once and are
                // now reading from that chunk.
                Assert.True(stream.Position >= bufferSize);
                continue;
            }

            // Second chunk
            if (offset < bufferSize * 2)
            {
                Assert.True(stream.Position > bufferSize);

                // Odd buffer size with even increments can
                // jump to the third chunk on final read.
                Assert.True(stream.Position <= bufferSize * 3);
                continue;
            }

            // Third chunk
            Assert.True(stream.Position > bufferSize * 2);
        }
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanReadSubsequentMultipleByteSpanCorrectly(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        const int increment = 2;
        Span<byte> buffer = new byte[2];
        byte[] expected = stream.ToArray();
        using BufferedReadStream reader = new(this.configuration, stream);
        for (int i = 0, o = 0; i < expected.Length / increment; i++, o += increment)
        {
            // Check values are correct.
            Assert.Equal(increment, reader.Read(buffer, 0, increment));
            Assert.Equal(expected[o], buffer[0]);
            Assert.Equal(expected[o + 1], buffer[1]);
            Assert.Equal(o + increment, reader.Position);

            // These tests ensure that we are correctly reading
            // our buffer in chunks of the given size.
            int offset = i * increment;

            // First chunk.
            if (offset < bufferSize)
            {
                // We've read an entire chunk once and are
                // now reading from that chunk.
                Assert.True(stream.Position >= bufferSize);
                continue;
            }

            // Second chunk
            if (offset < bufferSize * 2)
            {
                Assert.True(stream.Position > bufferSize);

                // Odd buffer size with even increments can
                // jump to the third chunk on final read.
                Assert.True(stream.Position <= bufferSize * 3);
                continue;
            }

            // Third chunk
            Assert.True(stream.Position > bufferSize * 2);
        }
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanSkip(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 4);
        byte[] expected = stream.ToArray();
        using BufferedReadStream reader = new(this.configuration, stream);
        const int skip = 1;
        const int plusOne = 1;
        int skip2 = bufferSize;

        // Skip
        reader.Skip(skip);
        Assert.Equal(skip, reader.Position);

        // Read
        Assert.Equal(expected[skip], reader.ReadByte());

        // Skip Again
        reader.Skip(skip2);

        // First Skip + First Read + Second Skip
        int position = skip + plusOne + skip2;

        Assert.Equal(position, reader.Position);
        Assert.Equal(expected[position], reader.ReadByte());
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamReadsSmallStream(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;

        // Create a stream smaller than the default buffer length
        using MemoryStream stream = CreateTestStream(Math.Max(1, bufferSize / 4));
        byte[] expected = stream.ToArray();
        int offset = expected.Length / 2;
        using (BufferedReadStream reader = new(this.configuration, stream))
        {
            reader.Position = offset;

            Assert.Equal(expected[offset], reader.ReadByte());

            // We've read a whole length of the stream but increment by 1 in our reader.
            Assert.Equal(Math.Max(1, bufferSize / 4), stream.Position);
            Assert.Equal(offset + 1, reader.Position);
        }

        Assert.Equal(offset + 1, stream.Position);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamReadsCanReadAllAsSingleByteFromOrigin(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 3);
        byte[] expected = stream.ToArray();
        using BufferedReadStream reader = new(this.configuration, stream);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], reader.ReadByte());
        }
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamThrowsOnNegativePosition(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize);
        using BufferedReadStream reader = new(this.configuration, stream);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = -stream.Length);
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanSetPositionToEnd(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 2);
        using BufferedReadStream reader = new(this.configuration, stream);
        reader.Position = reader.Length;
    }

    [Theory]
    [MemberData(nameof(BufferSizes))]
    public void BufferedStreamCanSetPositionPastTheEnd(int bufferSize)
    {
        this.configuration.StreamProcessingBufferSize = bufferSize;
        using MemoryStream stream = CreateTestStream(bufferSize * 2);
        using BufferedReadStream reader = new(this.configuration, stream);
        reader.Position = reader.Length + 1;
        Assert.Equal(stream.Length + 1, stream.Position);
    }

    [Fact]
    public void BufferedStreamCanSetPositionMultipleTimes()
    {
        Configuration configuration = new()
        {
            StreamProcessingBufferSize = 16
        };

        byte[] buffer = new byte[255];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)i;
        }

        BufferedReadStream bufferedStream = new(configuration, new MemoryStream(buffer));

        // Read more then fits into the buffer.
        for (int i = 0; i < 20; i++)
        {
            bufferedStream.ReadByte();
        }

        // Set the Position twice.
        bufferedStream.Position = 10;
        bufferedStream.Position = 3;

        int actual = bufferedStream.ReadByte();
        Assert.Equal(3, actual);
    }

    private static MemoryStream CreateTestStream(int length)
    {
        byte[] buffer = new byte[length];
        Random random = new();
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
            => base.Read(buffer, offset, 1);
    }
}
