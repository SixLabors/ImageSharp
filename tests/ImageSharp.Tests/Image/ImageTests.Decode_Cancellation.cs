// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Decode_Cancellation : ImageLoadTestBase
    {
        private bool isTestStreamSeekable;
        private readonly SemaphoreSlim notifyWaitPositionReachedSemaphore = new(0);
        private readonly SemaphoreSlim continueSemaphore = new(0);
        private readonly CancellationTokenSource cts = new();

        public Decode_Cancellation() => this.TopLevelConfiguration.StreamProcessingBufferSize = 128;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task LoadAsync_Specific_Stream(bool isInputStreamSeekable)
        {
            this.isTestStreamSeekable = isInputStreamSeekable;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync<Rgb24>(options, this.DataStream, this.cts.Token));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task LoadAsync_Agnostic_Stream(bool isInputStreamSeekable)
        {
            this.isTestStreamSeekable = isInputStreamSeekable;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync(options, this.DataStream, this.cts.Token));
        }

        [Fact]
        public Task LoadAsync_Agnostic_Path()
        {
            this.isTestStreamSeekable = true;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync(options, this.MockFilePath, this.cts.Token));
        }

        [Fact]
        public Task LoadAsync_Specific_Path()
        {
            this.isTestStreamSeekable = true;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.LoadAsync<Rgb24>(options, this.MockFilePath, this.cts.Token));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task IdentifyAsync_Stream(bool isInputStreamSeekable)
        {
            this.isTestStreamSeekable = isInputStreamSeekable;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyAsync(options, this.DataStream, this.cts.Token));
        }

        [Fact]
        public Task IdentifyAsync_CustomConfiguration_Path()
        {
            this.isTestStreamSeekable = true;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyAsync(options, this.MockFilePath, this.cts.Token));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task IdentifyWithFormatAsync_CustomConfiguration_Stream(bool isInputStreamSeekable)
        {
            this.isTestStreamSeekable = isInputStreamSeekable;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(options, this.DataStream, this.cts.Token));
        }

        [Fact]
        public Task IdentifyWithFormatAsync_CustomConfiguration_Path()
        {
            this.isTestStreamSeekable = true;
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(options, this.MockFilePath, this.cts.Token));
        }

        [Fact]
        public Task IdentifyWithFormatAsync_DefaultConfiguration_Stream()
        {
            _ = Task.Factory.StartNew(this.DoCancel, TaskCreationOptions.LongRunning);

            return Assert.ThrowsAsync<TaskCanceledException>(() => Image.IdentifyWithFormatAsync(this.DataStream, this.cts.Token));
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

        protected override Stream CreateStream() => this.TestFormat.CreateAsyncSemaphoreStream(this.notifyWaitPositionReachedSemaphore, this.continueSemaphore, this.isTestStreamSeekable);
    }
}
