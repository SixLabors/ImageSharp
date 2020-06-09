// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    // https://github.com/dotnet/aspnetcore/blob/620c673705bb17b33cbc5ff32872d85a5fbf82b9/src/Hosting/TestHost/src/AsyncStreamWrapper.cs
    internal class AsyncStreamWrapper : Stream
    {
        private Stream inner;
        private Func<bool> allowSynchronousIO;

        internal AsyncStreamWrapper(Stream inner, Func<bool> allowSynchronousIO)
        {
            this.inner = inner;
            this.allowSynchronousIO = allowSynchronousIO;
        }

        public override bool CanRead => this.inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => this.inner.CanWrite;

        public override long Length => this.inner.Length;

        public override long Position
        {
            get => throw new NotSupportedException("The stream is not seekable.");
            set => throw new NotSupportedException("The stream is not seekable.");
        }

        public override void Flush()
        {
            // Not blocking Flush because things like StreamWriter.Dispose() always call it.
            this.inner.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.inner.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this.allowSynchronousIO())
            {
                throw new InvalidOperationException("Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true.");
            }

            return this.inner.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this.inner.EndRead(asyncResult);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("The stream is not seekable.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("The stream is not seekable.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.allowSynchronousIO())
            {
                throw new InvalidOperationException("Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true.");
            }

            this.inner.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.inner.EndWrite(asyncResult);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Close()
        {
            // Don't dispose the inner stream, we don't want to impact the client stream
        }

        protected override void Dispose(bool disposing)
        {
            // Don't dispose the inner stream, we don't want to impact the client stream
        }
    }
}
