// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// A disposable writer for bytes in memory. It is highly similar to
/// <see cref="MemoryStream"/>, but its buffer relies on
/// <see cref="MemoryAllocator"/>.
/// </summary>
internal sealed class JxlMemoryWriter(MemoryAllocator allocator) : IDisposable
{
    /// <summary>
    /// The initial capacity in bytes.
    /// </summary>
    private const int InitialCapacity = 1024;

    /// <summary>
    /// Core buffer.
    /// </summary>
    private IMemoryOwner<byte> buffer = allocator.Allocate<byte>(InitialCapacity);

    /// <summary>
    /// Gets the length of the written data in bytes.
    /// </summary>
    public int Length { get; private set; }

    /// <summary>
    /// Gets the capacity of the buffer in bytes.
    /// </summary>
    public int Capacity => this.buffer.Memory.Length;

    /// <summary>
    /// Releases the underlying buffer.
    /// </summary>
    public void Dispose() => this.buffer.Dispose();

    /// <summary>
    /// Writes the specified bytes into the writer.
    /// </summary>
    /// <param name="bytes">The bytes to write.</param>
    public void Write(ReadOnlySpan<byte> bytes)
    {
        int requiredCapacity = checked(this.Length + bytes.Length);
        this.EnsureCapacity(requiredCapacity);

        bytes.CopyTo(this.buffer.Memory.Span[this.Length..]);
        this.Length = requiredCapacity;
    }

    /// <summary>
    /// Returns a span containing the bytes written to the writer.
    /// </summary>
    /// <returns>A span containing the written bytes.</returns>
    public Span<byte> AsSpan() => this.buffer.Memory.Span[..this.Length];

    /// <summary>
    /// Returns memory containing the bytes written to the writer.
    /// </summary>
    /// <returns>Memory containing the written bytes.</returns>
    public Memory<byte> AsMemory() => this.buffer.Memory[..this.Length];

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= this.Capacity)
        {
            return;
        }

        int newCapacity = Math.Max(requiredCapacity, checked(this.Capacity * 2));

        IMemoryOwner<byte> previousBuffer = this.buffer;
        this.buffer = allocator.Allocate<byte>(newCapacity);

        previousBuffer.Memory.Span[..this.Length].CopyTo(this.buffer.Memory.Span);

        previousBuffer.Dispose();
    }
}
