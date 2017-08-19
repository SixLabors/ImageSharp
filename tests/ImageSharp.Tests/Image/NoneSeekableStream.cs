// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Tests
{
    internal class NoneSeekableStream : Stream
    {
        private Stream dataStream;

        public NoneSeekableStream(Stream dataStream)
        {
            this.dataStream = dataStream;
        }

        public override bool CanRead => this.dataStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => this.dataStream.Length;

        public override long Position { get => this.dataStream.Position; set => throw new NotImplementedException(); }

        public override void Flush()
        {
            this.dataStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.dataStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}