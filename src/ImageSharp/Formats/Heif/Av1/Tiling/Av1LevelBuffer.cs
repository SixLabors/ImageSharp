// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the padded absolute-coefficient level plane used to derive AV1 coefficient entropy contexts.
/// </summary>
internal sealed partial class Av1LevelBuffer : IDisposable
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
        this.WidthLog2 = BitOperations.Log2((uint)size.Width);

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
    /// Gets the base-two logarithm of the unpadded width. Every entropy-coded transform width is a power of two,
    /// which lets a raster index split into its row and column with a shift and a mask instead of a division.
    /// </summary>
    public int WidthLog2 { get; private set; }

    /// <summary>
    /// Gets the coefficient level at the specified unpadded position.
    /// </summary>
    /// <param name="position">The coefficient position.</param>
    public int this[Point position] => this.GetRow(position.Y)[position.X];

    /// <summary>
    /// Initializes the level plane, its right padding, and its bottom padding from raster-ordered coefficients.
    /// </summary>
    /// <remarks>
    /// Context derivation reads only forward neighbors: up to four samples to the right and four rows down.
    /// Writing those regions here makes a preceding clear of the active layout unnecessary.
    /// </remarks>
    /// <param name="coefficientBuffer">The coefficient levels to copy.</param>
    public void Initialize(ReadOnlySpan<int> coefficientBuffer) => this.Initialize(this.GetStorage(), coefficientBuffer);

    /// <summary>
    /// Initializes the level plane, its right padding, and its bottom padding from raster-ordered coefficients, in
    /// storage that the caller read once with <see cref="GetStorage"/>.
    /// </summary>
    /// <param name="storage">All of the level storage, from <see cref="GetStorage"/>.</param>
    /// <param name="coefficientBuffer">The coefficient levels to copy.</param>
    public void Initialize(Span<byte> storage, ReadOnlySpan<int> coefficientBuffer)
    {
        int width = this.Size.Width;
        int height = this.Size.Height;
        ArgumentOutOfRangeException.ThrowIfLessThan(coefficientBuffer.Length, width * height, nameof(coefficientBuffer));

        int stride = this.Stride;
        Span<byte> levels = this.GetActiveLevels(storage);
        ref byte destinationBase = ref MemoryMarshal.GetReference(levels);
        ref int sourceBase = ref MemoryMarshal.GetReference(coefficientBuffer);

        if (width == 4 && Vector128.IsHardwareAccelerated)
        {
            // A row of four coefficients is too short for a vector, so four rows go together, padding included.
            Levels<LevelOperator>.FillFourWide(ref sourceBase, ref destinationBase, height);
        }
        else
        {
            nuint rowStep = (nuint)stride;
            nuint columns = (nuint)width;
            for (nuint y = 0; y < (nuint)height; y++)
            {
                ref byte destination = ref Unsafe.Add(ref destinationBase, y * rowStep);
                Levels<LevelOperator>.FillRow(ref Unsafe.Add(ref sourceBase, y * columns), ref destination, width);

                // The four padding bytes after each row are the right-hand neighbors of its final columns.
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, columns), 0u);
            }
        }

        // Rows below the transform are the lower neighbors of its final rows.
        levels.Slice(height * stride, Av1Constants.TransformPadBottom * stride).Clear();
    }

    /// <summary>
    /// Gets the active level plane, starting at the first coded row and including the bottom context padding.
    /// </summary>
    /// <remarks>
    /// Hot paths fetch this span once per transform block and address it with <see cref="GetPaddedIndex"/>.
    /// Resolving the owned memory for every neighbor read costs more than the context arithmetic itself.
    /// </remarks>
    /// <returns>The padded rows of the active layout.</returns>
    public Span<byte> GetActiveLevels() => this.GetActiveLevels(this.GetStorage());

    /// <summary>
    /// Gets the active level plane from storage that the caller read once with <see cref="GetStorage"/>.
    /// </summary>
    /// <param name="storage">All of the level storage, from <see cref="GetStorage"/>.</param>
    /// <returns>The padded rows of the active layout.</returns>
    public Span<byte> GetActiveLevels(Span<byte> storage)
        => storage.Slice(Av1Constants.TransformPadTop * this.Stride, (this.Size.Height + Av1Constants.TransformPadBottom) * this.Stride);

    /// <summary>
    /// Gets all of the level storage. A caller that codes many transform blocks reads it once and passes it to
    /// <see cref="Initialize(Span{byte}, ReadOnlySpan{int})"/> and <see cref="GetActiveLevels(Span{byte})"/>.
    /// </summary>
    /// <returns>The whole level storage.</returns>
    public Span<byte> GetStorage()
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        return this.memory.Memory.Span;
    }

    /// <summary>
    /// Converts a raster-order coefficient index to its offset in the span from <see cref="GetActiveLevels(Span{byte})"/>.
    /// </summary>
    /// <param name="index">The raster-order coefficient index.</param>
    /// <param name="widthLog2">The base-two logarithm of the unpadded width.</param>
    /// <returns>The offset that accounts for the horizontal padding of every preceding row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPaddedIndex(int index, int widthLog2)
        => index + ((index >> widthLog2) << Av1Constants.TransformPadHorizontalLog2);

    /// <summary>
    /// Converts a raster-order coefficient index to its two-dimensional position.
    /// </summary>
    /// <param name="index">The raster-order coefficient index.</param>
    /// <returns>The corresponding coefficient position.</returns>
    public Point GetPosition(int index)
    {
        // The width is a power of two, so the row and column split without a division.
        int x = index & (this.Size.Width - 1);
        int y = index >> this.WidthLog2;
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
    public void Reset(Size size) => this.Reset(size, clear: true);

    /// <summary>
    /// Selects new active coefficient dimensions and optionally clears their padded context storage.
    /// </summary>
    /// <param name="size">The unpadded coefficient dimensions.</param>
    /// <param name="clear">Indicates whether to clear the active level plane and its context padding.</param>
    public void Reset(Size size, bool clear)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        this.Size = size;
        this.WidthLog2 = BitOperations.Log2((uint)size.Width);
        this.Stride = Av1Constants.TransformPadHorizontal + size.Width;

        if (clear)
        {
            // Tile parsing is sequential, so one maximum-sized rent can serve every transform. Clear only the active
            // layout because stale neighboring levels would otherwise select the wrong coefficient distributions.
            int totalHeight = Av1Constants.TransformPadTop + size.Height + Av1Constants.TransformPadBottom;
            this.memory.Memory.Span[..(this.Stride * totalHeight)].Clear();
        }
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
