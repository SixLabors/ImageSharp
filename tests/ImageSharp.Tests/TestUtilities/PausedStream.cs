// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    public class PausedStream : Stream
    {
        private readonly SemaphoreSlim slim = new SemaphoreSlim(0);

        private readonly CancellationTokenSource cancelationTokenSource = new CancellationTokenSource();

        private readonly Stream innerStream;
        private Action<Stream> onWaitingCallback;

        public void OnWaiting(Action<Stream> onWaitingCallback) => this.onWaitingCallback = onWaitingCallback;

        public void OnWaiting(Action onWaitingCallback) => this.OnWaiting(_ => onWaitingCallback());

        public void Release()
        {
            this.slim.Release();
            this.cancelationTokenSource.Cancel();
        }

        public void Next() => this.slim.Release();

        private void Wait()
        {
            if (this.cancelationTokenSource.IsCancellationRequested)
            {
                return;
            }

            this.onWaitingCallback?.Invoke(this.innerStream);

            try
            {
                this.slim.Wait(this.cancelationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // ignore this as its just used to unlock any waits in progress
            }
        }

        private async Task Await(Func<Task> action)
        {
            await Task.Yield();
            this.Wait();
            await action();
        }

        private async Task<T> Await<T>(Func<Task<T>> action)
        {
            await Task.Yield();
            this.Wait();
            return await action();
        }

        private T Await<T>(Func<T> action)
        {
            this.Wait();
            return action();
        }

        private void Await(Action action)
        {
            this.Wait();
            action();
        }

        public PausedStream(byte[] data)
            : this(new MemoryStream(data))
        {
        }

        public PausedStream(string filePath)
            : this(File.OpenRead(filePath))
        {
        }

        public PausedStream(Stream innerStream) => this.innerStream = innerStream;

        public override bool CanTimeout => this.innerStream.CanTimeout;

        public override void Close() => this.Await(() => this.innerStream.Close());

        public override void CopyTo(Stream destination, int bufferSize) => this.Await(() => this.innerStream.CopyTo(destination, bufferSize));

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => this.Await(() => this.innerStream.CopyToAsync(destination, bufferSize, cancellationToken));

        public override bool CanRead => this.innerStream.CanRead;

        public override bool CanSeek => this.innerStream.CanSeek;

        public override bool CanWrite => this.innerStream.CanWrite;

        public override long Length => this.Await(() => this.innerStream.Length);

        public override long Position { get => this.Await(() => this.innerStream.Position); set => this.Await(() => this.innerStream.Position = value); }

        public override void Flush() => this.Await(() => this.innerStream.Flush());

        public override int Read(byte[] buffer, int offset, int count) => this.Await(() => this.innerStream.Read(buffer, offset, count));

        public override long Seek(long offset, SeekOrigin origin) => this.Await(() => this.innerStream.Seek(offset, origin));

        public override void SetLength(long value) => this.Await(() => this.innerStream.SetLength(value));

        public override void Write(byte[] buffer, int offset, int count) => this.Await(() => this.innerStream.Write(buffer, offset, count));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.Await(() => this.innerStream.ReadAsync(buffer, offset, count, cancellationToken));

        public override int Read(Span<byte> buffer)
        {
            this.Wait();
            return this.innerStream.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.Await(() => this.innerStream.ReadAsync(buffer, cancellationToken));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.Wait();
            this.innerStream.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.Await(() => this.innerStream.WriteAsync(buffer, offset, count, cancellationToken));

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => this.Await(() => this.innerStream.WriteAsync(buffer, cancellationToken));

        public override void WriteByte(byte value) => this.Await(() => this.innerStream.WriteByte(value));

        public override int ReadByte() => this.Await(() => this.innerStream.ReadByte());

        protected override void Dispose(bool disposing) => this.innerStream.Dispose();
    }
}
