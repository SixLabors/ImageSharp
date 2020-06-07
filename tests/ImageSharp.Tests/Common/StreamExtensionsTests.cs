// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Common
{
    public class StreamExtensionsTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Skip_CountZeroOrLower_PositionNotChanged(int count)
        {
            using (var memStream = new MemoryStream(5))
            {
                memStream.Position = 4;
                memStream.Skip(count);

                Assert.Equal(4, memStream.Position);
            }
        }

        [Fact]
        public void Skip_SeekableStream_SeekIsCalled()
        {
            using (var seekableStream = new SeekableStream(4))
            {
                seekableStream.Skip(4);

                Assert.Equal(4, seekableStream.Offset);
                Assert.Equal(SeekOrigin.Current, seekableStream.Loc);
            }
        }

        [Fact]
        public void Skip_NonSeekableStream_BytesAreRead()
        {
            using (var nonSeekableStream = new NonSeekableStream())
            {
                nonSeekableStream.Skip(5);

                Assert.Equal(3, nonSeekableStream.Counts.Count);

                Assert.Equal(5, nonSeekableStream.Counts[0]);
                Assert.Equal(3, nonSeekableStream.Counts[1]);
                Assert.Equal(1, nonSeekableStream.Counts[2]);
            }
        }

        [Fact]
        public void Skip_EofStream_NoExceptionIsThrown()
        {
            using (var eofStream = new EofStream(7))
            {
                eofStream.Skip(7);

                Assert.Equal(0, eofStream.Position);
            }
        }

        private class SeekableStream : MemoryStream
        {
            public long Offset;
            public SeekOrigin Loc;

            public SeekableStream(int capacity)
                : base(capacity)
            {
            }

            public override long Seek(long offset, SeekOrigin loc)
            {
                this.Offset = offset;
                this.Loc = loc;
                return base.Seek(offset, loc);
            }
        }

        private class NonSeekableStream : MemoryStream
        {
            public override bool CanSeek => false;

            public List<int> Counts = new List<int>();

            public NonSeekableStream()
                : base(4)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.Counts.Add(count);

                return Math.Min(2, count);
            }
        }

        private class EofStream : MemoryStream
        {
            public override bool CanSeek => false;

            public EofStream(int capacity)
                : base(capacity)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return 0;
            }
        }
    }
}
