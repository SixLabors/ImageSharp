// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the padded absolute-coefficient level plane used to derive AV1 coefficient entropy contexts.
/// </summary>
internal sealed class Av1LevelBuffer : IDisposable
{
    /// <summary>
    /// Owns the padded level storage until the buffer is disposed.
    /// </summary>
    private IMemoryOwner<byte>? memory;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LevelBuffer"/> class for the maximum entropy-coded
    /// coefficient dimensions.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    public Av1LevelBuffer(Configuration configuration)
        : this(
            configuration,
            new Size(Av1Constants.MaxTransformSize / 2, Av1Constants.MaxTransformSize / 2))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LevelBuffer"/> class for the specified coefficient dimensions.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    /// <param name="size">The unpadded coefficient dimensions.</param>
    public Av1LevelBuffer(Configuration configuration, Size size)
    {
        this.Size = size;

        // Coefficient-context derivation reads fixed neighboring offsets around the coded transform.
        // Keeping those offsets inside one clean allocation avoids branches at transform boundaries.
        int totalHeight = Av1Constants.TransformPadTop + size.Height + Av1Constants.TransformPadBottom;
        this.Stride = Av1Constants.TransformPadHorizontal + size.Width;
        this.memory = configuration.MemoryAllocator.Allocate<byte>(this.Stride * totalHeight, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets the unpadded coefficient dimensions.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets the padded row stride in bytes.
    /// </summary>
    public int Stride { get; private set; }

    /// <summary>
    /// Gets the coefficient level at the specified unpadded position.
    /// </summary>
    /// <param name="position">The coefficient position.</param>
    public int this[Point position] => this.GetRow(position.Y)[position.X];

    /// <summary>
    /// Initializes the unpadded level plane from raster-ordered coefficient magnitudes.
    /// </summary>
    /// <param name="coefficientBuffer">The coefficient levels to copy.</param>
    public void Initialize(Span<int> coefficientBuffer)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(coefficientBuffer.Length, this.Size.Width * this.Size.Height, nameof(coefficientBuffer));
        for (int y = 0; y < this.Size.Height; y++)
        {
            ref byte destRef = ref this.GetRow(y)[0];
            ref int sourceRef = ref coefficientBuffer[y * this.Size.Width];
            for (int x = 0; x < this.Size.Width; x++)
            {
                // Entropy contexts use the absolute level, saturated to the signed-byte range used by the
                // normative nonzero-map context calculation.
                destRef = (byte)Math.Min(Math.Abs(sourceRef), sbyte.MaxValue);
                destRef = ref Unsafe.Add(ref destRef, 1);
                sourceRef = ref Unsafe.Add(ref sourceRef, 1);
            }
        }
    }

    /// <summary>
    /// Converts a raster-order coefficient index to its two-dimensional position.
    /// </summary>
    /// <param name="index">The raster-order coefficient index.</param>
    /// <returns>The corresponding coefficient position.</returns>
    public Point GetPosition(int index)
    {
        int x = index % this.Size.Width;
        int y = index / this.Size.Width;
        return new Point(x, y);
    }

    /// <summary>
    /// Gets a padded coefficient row for the specified position.
    /// </summary>
    /// <param name="pos">A position whose vertical coordinate selects the row.</param>
    /// <returns>The selected row, including its horizontal context padding.</returns>
    public Span<byte> GetRow(Point pos)
        => this.GetRow(pos.Y);

    /// <summary>
    /// Gets a padded coefficient row by its unpadded vertical coordinate.
    /// </summary>
    /// <param name="y">The row coordinate, which may address the top context padding.</param>
    /// <returns>The selected row, including its horizontal context padding.</returns>
    public Span<byte> GetRow(int y)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(y, -Av1Constants.TransformPadTop);
        int row = y + Av1Constants.TransformPadTop;
        return this.memory.Memory.Span.Slice(row * this.Stride, this.Size.Width + Av1Constants.TransformPadHorizontal);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.memory?.Dispose();
        this.memory = null;
    }

    /// <summary>
    /// Selects new active coefficient dimensions and clears their padded context storage.
    /// </summary>
    /// <param name="size">The unpadded coefficient dimensions.</param>
    public void Reset(Size size)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        this.Size = size;
        this.Stride = Av1Constants.TransformPadHorizontal + size.Width;

        // Tile parsing is sequential, so one maximum-sized rent can serve every transform. Clear only the active
        // layout because stale neighboring levels would otherwise select the wrong coefficient distributions.
        int totalHeight = Av1Constants.TransformPadTop + size.Height + Av1Constants.TransformPadBottom;
        this.memory.Memory.Span[..(this.Stride * totalHeight)].Clear();
    }

    /// <summary>
    /// Clears all coefficient levels and context padding.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        this.memory.Memory.Span.Clear();
    }
}
