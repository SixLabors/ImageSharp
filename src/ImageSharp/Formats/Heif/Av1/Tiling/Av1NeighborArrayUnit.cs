// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores left and top neighbor values at the granularity required by AV1 encoder contexts.
/// </summary>
/// <typeparam name="T">The context value type.</typeparam>
internal sealed class Av1NeighborArrayUnit<T> : IDisposable
    where T : struct
{
    /// <summary>
    /// Owns the contiguous neighbor storage until this instance is disposed.
    /// </summary>
    private IMemoryOwner<T>? owner;

    /// <summary>
    /// The contiguous neighbor storage, whether owned directly or supplied by a picture owner.
    /// </summary>
    private Memory<T> memory;

    /// <summary>
    /// Indicates whether the neighbor view has been disposed.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// The number of context values exposed to blocks on the right.
    /// </summary>
    private readonly int leftLength;

    /// <summary>
    /// The number of context values exposed to blocks below.
    /// </summary>
    private readonly int topLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborArrayUnit{T}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="leftSize">The number of values in the left-neighbor storage.</param>
    /// <param name="topSize">The number of values in the top-neighbor storage.</param>
    public Av1NeighborArrayUnit(Configuration configuration, int leftSize, int topSize)
    {
        this.leftLength = leftSize;
        this.topLength = topSize;
        int totalLength = checked(leftSize + topSize);

        // Both context edges have the picture lifetime. One clean allocation holds both edges and gives them their zero start state.
        this.owner = configuration.MemoryAllocator.Allocate<T>(totalLength, AllocationOptions.Clean);
        this.memory = this.owner.Memory[..totalLength];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1NeighborArrayUnit{T}"/> class over non-owning picture-lifetime storage.
    /// </summary>
    /// <param name="memory">The contiguous left and top context storage.</param>
    /// <param name="leftSize">The number of values in the left-neighbor storage.</param>
    /// <param name="topSize">The number of values in the top-neighbor storage.</param>
    public Av1NeighborArrayUnit(Memory<T> memory, int leftSize, int topSize)
    {
        this.leftLength = leftSize;
        this.topLength = topSize;
        this.memory = memory[..checked(leftSize + topSize)];
    }

    /// <summary>
    /// Selects which neighbor arrays receive an update.
    /// </summary>
    [Flags]
    public enum UnitMask
    {
        /// <summary>
        /// Update the left-neighbor storage.
        /// </summary>
        Left = 1,

        /// <summary>
        /// Update the top-neighbor storage.
        /// </summary>
        Top = 2,
    }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the top and left context granularity in samples.
    /// </summary>
    public required int GranularityNormalLog2 { get; set; }

    /// <summary>
    /// Reads the storage once and returns both edges, for a caller that passes them to the code of many blocks.
    /// </summary>
    /// <returns>The top and left edges with their unit size.</returns>
    public Av1NeighborEdges<T> GetEdges()
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        Span<T> storage = this.memory.Span;
        return new Av1NeighborEdges<T>(storage.Slice(this.leftLength, this.topLength), storage[..this.leftLength], this.GranularityNormalLog2);
    }

    /// <summary>
    /// Clears both edges, as the start of a tile requires.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        this.memory.Span.Clear();
    }

    /// <summary>
    /// Sets every unit of both edges to one value, as the start of a tile requires.
    /// </summary>
    /// <param name="value">The value to store.</param>
    public void Fill(T value)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        this.memory.Span.Fill(value);
    }

    /// <summary>
    /// Returns the neighbor storage to the configured memory allocator.
    /// </summary>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
        this.memory = Memory<T>.Empty;
        this.isDisposed = true;
    }
}
