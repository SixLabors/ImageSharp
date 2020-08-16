// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Tests
{
    internal class NonSeekableStream : Stream
    {
        private readonly Stream dataStream;

        public NonSeekableStream(Stream dataStream)
            => this.dataStream = dataStream;

        public override bool CanRead => this.dataStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush() => this.dataStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => this.dataStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotImplementedException();
    }
}
