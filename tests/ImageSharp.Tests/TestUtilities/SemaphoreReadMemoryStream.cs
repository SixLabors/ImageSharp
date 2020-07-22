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
        private SemaphoreSlim waitSemaphore;
        private readonly SemaphoreSlim signalFinishedSemaphore;
        private readonly long waitAfterPosition;

        public SemaphoreReadMemoryStream(byte[] buffer, SemaphoreSlim waitSemaphore, SemaphoreSlim signalFinishedSemaphore, long waitAfterPosition)
            : base(buffer)
        {
            this.waitSemaphore = waitSemaphore;
            this.signalFinishedSemaphore = signalFinishedSemaphore;
            this.waitAfterPosition = waitAfterPosition;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            if (this.Position + read > this.waitAfterPosition)
            {
                this.waitSemaphore.Wait();
            }

            this.SignalIfFinished();

            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await base.ReadAsync(buffer, offset, count, cancellationToken);
            if (this.Position + read > this.waitAfterPosition)
            {
                await this.waitSemaphore.WaitAsync();
            }

            this.SignalIfFinished();

            return read;
        }

        public override int ReadByte()
        {
            if (this.Position + 1 > this.waitAfterPosition)
            {
                this.waitSemaphore.Wait();
            }

            int result = base.ReadByte();
            this.SignalIfFinished();
            return result;
        }

        private void SignalIfFinished()
        {
            if (this.Position == this.Length)
            {
                this.signalFinishedSemaphore.Release();
            }
        }
    }
}
