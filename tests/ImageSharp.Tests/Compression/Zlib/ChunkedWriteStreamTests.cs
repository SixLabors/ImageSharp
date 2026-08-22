// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;

namespace SixLabors.ImageSharp.Tests.Compression.Zlib;

public class ChunkedWriteStreamTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(1000)]
    public void Write_EmitsFixedLengthSegments_AndPartialTailOnDispose(int writeSize)
    {
        const int SegmentLength = 64;
        byte[] data = new byte[250];
        new Random(42).NextBytes(data);

        List<byte[]> segments = [];
        using (ChunkedWriteStream stream = new(Configuration.Default.MemoryAllocator, SegmentLength, segment => segments.Add(segment.ToArray())))
        {
            for (int offset = 0; offset < data.Length; offset += writeSize)
            {
                stream.Write(data, offset, Math.Min(writeSize, data.Length - offset));
            }

            // Nothing but full segments is emitted before disposal.
            Assert.Equal(3, segments.Count);
            Assert.All(segments, s => Assert.Equal(SegmentLength, s.Length));
        }

        Assert.Equal(4, segments.Count);
        Assert.Equal(250 - (3 * SegmentLength), segments[3].Length);
        Assert.Equal(data, segments.SelectMany(s => s).ToArray());
    }

    [Fact]
    public void Write_ExactMultipleOfSegmentLength_DoesNotEmitEmptyTail()
    {
        const int SegmentLength = 16;
        byte[] data = new byte[SegmentLength * 3];

        int count = 0;
        using (ChunkedWriteStream stream = new(Configuration.Default.MemoryAllocator, SegmentLength, _ => count++))
        {
            stream.Write(data);
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void WriteByte_FillsSegments()
    {
        const int SegmentLength = 4;
        List<byte[]> segments = [];
        using (ChunkedWriteStream stream = new(Configuration.Default.MemoryAllocator, SegmentLength, segment => segments.Add(segment.ToArray())))
        {
            for (byte i = 0; i < 6; i++)
            {
                stream.WriteByte(i);
            }
        }

        Assert.Equal(2, segments.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, segments[0]);
        Assert.Equal(new byte[] { 4, 5 }, segments[1]);
    }

    [Fact]
    public void Flush_DoesNotEmitPartialSegment()
    {
        int count = 0;
        using (ChunkedWriteStream stream = new(Configuration.Default.MemoryAllocator, 16, _ => count++))
        {
            stream.Write(new byte[5]);
            stream.Flush();
            Assert.Equal(0, count);
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dispose_WithoutWrites_EmitsNothing()
    {
        int count = 0;
        using (ChunkedWriteStream stream = new(Configuration.Default.MemoryAllocator, 16, _ => count++))
        {
        }

        Assert.Equal(0, count);
    }
}
