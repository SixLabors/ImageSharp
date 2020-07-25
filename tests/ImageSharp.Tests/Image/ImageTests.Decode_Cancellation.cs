// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Decode_Cancellation : ImageLoadTestBase
        {
            private bool isTestStreamSeekable;
            private readonly SemaphoreSlim notifyWaitPositionReachedSemaphore = new SemaphoreSlim(0);
            private readonly SemaphoreSlim continueSemaphore = new SemaphoreSlim(0);
            private readonly CancellationTokenSource cts = new CancellationTokenSource();

            public Decode_Cancellation()
            {
                this.TopLevelConfiguration.StreamProcessingBufferSize = 128;
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task LoadAsync_Specific_Stream(bool isInputStreamSeekable)
            {
                this.isTestStreamSeekable = isInputStreamSeekable;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync<Rgb24>(this.TopLevelConfiguration, this.DataStream, this.cts.Token));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task LoadAsync_Agnostic_Stream(bool isInputStreamSeekable)
            {
                this.isTestStreamSeekable = isInputStreamSeekable;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync(this.TopLevelConfiguration, this.DataStream, this.cts.Token));
            }

            [Fact]
            public async Task LoadAsync_Agnostic_Path()
            {
                this.isTestStreamSeekable = true;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync(this.TopLevelConfiguration, this.MockFilePath, this.cts.Token));
            }

            [Fact]
            public async Task LoadAsync_Specific_Path()
            {
                this.isTestStreamSeekable = true;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync<Rgb24>(this.TopLevelConfiguration, this.MockFilePath, this.cts.Token));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task IdentifyAsync_Stream(bool isInputStreamSeekable)
            {
                this.isTestStreamSeekable = isInputStreamSeekable;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyAsync(this.TopLevelConfiguration, this.DataStream, this.cts.Token));
            }

            [Fact]
            public async Task IdentifyAsync_CustomConfiguration_Path()
            {
                this.isTestStreamSeekable = true;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyAsync(this.TopLevelConfiguration, this.MockFilePath, this.cts.Token));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task IdentifyWithFormatAsync_CustomConfiguration_Stream(bool isInputStreamSeekable)
            {
                this.isTestStreamSeekable = isInputStreamSeekable;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(this.TopLevelConfiguration, this.DataStream, this.cts.Token));
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_CustomConfiguration_Path()
            {
                this.isTestStreamSeekable = true;
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(this.TopLevelConfiguration, this.MockFilePath, this.cts.Token));
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_DefaultConfiguration_Stream()
            {
                _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

                await Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(this.DataStream, this.cts.Token));
            }

            private async Task DoCancel()
            {
                // wait until we reach the middle of the steam
                await this.notifyWaitPositionReachedSemaphore.WaitAsync();

                // set the cancellation
                this.cts.Cancel();

                // continue processing the stream
                this.continueSemaphore.Release();
            }

            protected override Stream CreateStream() => this.TestFormat.CreateAsyncSamaphoreStream(this.notifyWaitPositionReachedSemaphore, this.continueSemaphore, this.isTestStreamSeekable);
        }
    }
}
