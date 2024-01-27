// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// A stack only, readonly, kernel matrix that can be indexed without
/// bounds checks when compiled in release mode.
/// </summary>
/// <typeparam name="T">The type of each element in the kernel.</typeparam>
internal readonly ref struct Kernel<T>
    where T : struct, IEquatable<T>
{
    private readonly Span<T> values;

    public Kernel(DenseMatrix<T> matrix)
    {
        this.Columns = matrix.Columns;
        this.Rows = matrix.Rows;
        this.values = matrix.Span;
    }

    public int Columns
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public int Rows
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.values;
    }

    public T this[int row, int column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            this.CheckCoordinates(row, column);
            ref T vBase = ref MemoryMarshal.GetReference(this.values);
            return Extensions.UnsafeAdd(ref vBase, (uint)((row * this.Columns) + column));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            this.CheckCoordinates(row, column);
            ref T vBase = ref MemoryMarshal.GetReference(this.values);
            Extensions.UnsafeAdd(ref vBase, (uint)((row * this.Columns) + column)) = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(int index, T value)
    {
        this.CheckIndex(index);
        ref T vBase = ref MemoryMarshal.GetReference(this.values);
        Extensions.UnsafeAdd(ref vBase, (uint)index) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => this.values.Clear();

    [Conditional("DEBUG")]
    private void CheckCoordinates(int row, int column)
    {
        if (row < 0 || row >= this.Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"{row} is outside the matrix bounds.");
        }

        if (column < 0 || column >= this.Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"{column} is outside the matrix bounds.");
        }
    }

    [Conditional("DEBUG")]
    private void CheckIndex(int index)
    {
        if (index < 0 || index >= this.values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{index} is outside the matrix bounds.");
        }
    }
}
