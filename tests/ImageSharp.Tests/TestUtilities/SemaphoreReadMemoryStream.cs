// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    internal class SemaphoreReadMemoryStream : MemoryStream
    {
        private readonly SemaphoreSlim continueSemaphore;
        private readonly SemaphoreSlim notifyWaitPositionReachedSemaphore;
        private int pauseDone;
        private readonly long waitPosition;

        public SemaphoreReadMemoryStream(
            byte[] buffer,
            long waitPosition,
            SemaphoreSlim notifyWaitPositionReachedSemaphore,
            SemaphoreSlim continueSemaphore)
            : base(buffer)
        {
            this.continueSemaphore = continueSemaphore;
            this.notifyWaitPositionReachedSemaphore = notifyWaitPositionReachedSemaphore;
            this.waitPosition = waitPosition;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            if (this.Position > this.waitPosition && this.TryPause())
            {
                this.notifyWaitPositionReachedSemaphore.Release();
                this.continueSemaphore.Wait();
            }

            return read;
        }

        private bool TryPause() => Interlocked.CompareExchange(ref this.pauseDone, 1, 0) == 0;

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await base.ReadAsync(buffer, offset, count, cancellationToken);
            if (this.Position > this.waitPosition && this.TryPause())
            {
                this.notifyWaitPositionReachedSemaphore.Release();
                await this.continueSemaphore.WaitAsync();
            }

            return read;
        }

        public override int ReadByte()
        {
            if (this.Position + 1 > this.waitPosition && this.TryPause())
            {
                this.notifyWaitPositionReachedSemaphore.Release();
                this.continueSemaphore.Wait();
            }

            int result = base.ReadByte();
            return result;
        }
    }
}
