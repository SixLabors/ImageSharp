// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

/// <summary>
/// <see cref="PausedMemoryStream"/> is a variant of <see cref="PausedStream"/> that derives from
/// <see cref="MemoryStream"/> instead of encapsulating it.
/// It is used to test decoder cancellation without relying on of our standard prefetching of arbitrary streams
/// to <see cref="ImageSharp.IO.ChunkedMemoryStream"/> on asynchronous path.
/// </summary>
public class PausedMemoryStream : MemoryStream, IPausedStream
{
    private readonly SemaphoreSlim semaphore = new(0);

    private readonly CancellationTokenSource cancelationTokenSource = new();

    private Action<Stream> onWaitingCallback;

    public void OnWaiting(Action<Stream> onWaitingCallback) => this.onWaitingCallback = onWaitingCallback;

    public void OnWaiting(Action onWaitingCallback) => this.OnWaiting(_ => onWaitingCallback());

    public void Release()
    {
        this.semaphore.Release();
        this.cancelationTokenSource.Cancel();
    }

    public void Next() => this.semaphore.Release();

    private void Wait()
    {
        if (this.cancelationTokenSource.IsCancellationRequested)
        {
            return;
        }

        this.onWaitingCallback?.Invoke(this);

        try
        {
            this.semaphore.Wait(this.cancelationTokenSource.Token);
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

    public PausedMemoryStream(byte[] data)
        : base(data)
    {
    }

    public override bool CanTimeout => base.CanTimeout;

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        // To make sure the copy operation is buffered and pausable, we should override MemoryStream's strategy
        // with the default Stream copy logic of System.IO.Stream:
        // https://github.com/dotnet/runtime/blob/4f53c2f7e62df44f07cf410df8a0d439f42a0a71/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L104-L116
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await this.ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override bool CanRead => base.CanRead;

    public override bool CanSeek => base.CanSeek;

    public override bool CanWrite => base.CanWrite;

    public override void Flush() => this.Await(base.Flush);

    public override int Read(byte[] buffer, int offset, int count) => this.Await(() => base.Read(buffer, offset, count));

    public override long Seek(long offset, SeekOrigin loc) => this.Await(() => base.Seek(offset, loc));

    public override void SetLength(long value) => this.Await(() => base.SetLength(value));

    public override void Write(byte[] buffer, int offset, int count) => this.Await(() => base.Write(buffer, offset, count));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.Await(() => base.ReadAsync(buffer, offset, count, cancellationToken));

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.Await(() => base.WriteAsync(buffer, offset, count, cancellationToken));

    public override void WriteByte(byte value) => this.Await(() => base.WriteByte(value));

    public override int ReadByte() => this.Await(base.ReadByte);

    public override void CopyTo(Stream destination, int bufferSize)
    {
        // See comments on CopyToAsync.
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = this.Read(buffer, 0, buffer.Length)) != 0)
            {
                destination.Write(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override int Read(Span<byte> buffer)
    {
        this.Wait();
        return base.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.Await(() => base.ReadAsync(buffer, cancellationToken));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.Wait();
        base.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => this.Await(() => base.WriteAsync(buffer, cancellationToken));
}
