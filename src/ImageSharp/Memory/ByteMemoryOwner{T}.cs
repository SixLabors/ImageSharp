// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// A custom <see cref="IMemoryOwner{T}"/> that can wrap <see cref="IMemoryOwner{T}"/> of <see cref="byte"/> instances
/// and cast them to be <see cref="IMemoryOwner{T}"/> for any arbitrary unmanaged <typeparamref name="T"/> value type.
/// </summary>
/// <typeparam name="T">The value type to use when casting the wrapped <see cref="IMemoryOwner{T}"/> instance.</typeparam>
internal sealed class ByteMemoryOwner<T> : IMemoryOwner<T>
    where T : unmanaged
{
    private readonly IMemoryOwner<byte> memoryOwner;
    private readonly ByteMemoryManager<T> memoryManager;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteMemoryOwner{T}"/> class.
    /// </summary>
    /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> of <see cref="byte"/> instance to wrap.</param>
    public ByteMemoryOwner(IMemoryOwner<byte> memoryOwner)
    {
        this.memoryOwner = memoryOwner;
        this.memoryManager = new(memoryOwner.Memory);
    }

    /// <inheritdoc/>
    public Memory<T> Memory => this.memoryManager.Memory;

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.memoryOwner.Dispose();
            }

            this.disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
    }
}
