// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class SemaphoreReadMemoryStreamTests
    {
        private readonly SemaphoreSlim WaitSemaphore = new SemaphoreSlim(0);
        private readonly SemaphoreSlim FinishedSemaphore = new SemaphoreSlim(0);
        private readonly byte[] Buffer = new byte[128];

        [Fact]
        public void Read_BeforeWaitLimit_ShouldFinish()
        {
            using Stream stream = this.GetStream();
            int read = stream.Read(this.Buffer);
            Assert.Equal(this.Buffer.Length, read);
        }

        [Fact]
        public async Task ReadAsync_BeforeWaitLimit_ShouldFinish()
        {
            using Stream stream = this.GetStream();
            int read = await stream.ReadAsync(this.Buffer);
            Assert.Equal(this.Buffer.Length, read);
        }

        [Fact]
        public async Task Read_AfterWaitLimit_ShouldPause()
        {
            using Stream stream = this.GetStream();
            stream.Read(this.Buffer);

            Task readTask = Task.Factory.StartNew(() => stream.Read(new byte[512]), TaskCreationOptions.LongRunning);
            await Task.Delay(5);
            Assert.False(readTask.IsCompleted);
            this.WaitSemaphore.Release();
            await readTask;
        }

        [Fact]
        public async Task ReadAsync_AfterWaitLimit_ShouldPause()
        {
            using Stream stream = this.GetStream();
            await stream.ReadAsync(this.Buffer);

            Task readTask =
                Task.Factory.StartNew(() => stream.ReadAsync(new byte[512]).AsTask(), TaskCreationOptions.LongRunning);
            await Task.Delay(5);
            Assert.False(readTask.IsCompleted);
            this.WaitSemaphore.Release();
            await readTask;
        }

        [Fact]
        public async Task Read_WhenFinished_ShouldNotify()
        {
            using Stream stream = this.GetStream(512, int.MaxValue);
            stream.Read(this.Buffer);
            stream.Read(this.Buffer);
            stream.Read(this.Buffer);
            Assert.Equal(0, this.FinishedSemaphore.CurrentCount);
            stream.Read(this.Buffer);
            Assert.Equal(1, this.FinishedSemaphore.CurrentCount);
        }

        [Fact]
        public async Task ReadAsync_WhenFinished_ShouldNotify()
        {
            using Stream stream = this.GetStream(512, int.MaxValue);
            await stream.ReadAsync(this.Buffer);
            await stream.ReadAsync(this.Buffer);
            await stream.ReadAsync(this.Buffer);
            Assert.Equal(0, this.FinishedSemaphore.CurrentCount);

            Task lastRead = stream.ReadAsync(this.Buffer).AsTask();
            Task finishedTask = this.FinishedSemaphore.WaitAsync();
            await Task.WhenAll(lastRead, finishedTask);
        }

        private Stream GetStream(int size = 1024, int waitAfterPosition = 256)
        {
            byte[] buffer = new byte[size];
            return new SemaphoreReadMemoryStream(buffer, this.WaitSemaphore, this.FinishedSemaphore, waitAfterPosition);
        }
    }
}
