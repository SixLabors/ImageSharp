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
internal readonly ref struct ReadOnlyKernel
{
    private readonly ReadOnlySpan<float> values;

    public ReadOnlyKernel(DenseMatrix<float> matrix)
    {
        this.Columns = (uint)matrix.Columns;
        this.Rows = (uint)matrix.Rows;
        this.values = matrix.Span;
    }

    public uint Columns
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public uint Rows
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public float this[uint row, uint column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            this.CheckCoordinates(row, column);
            ref float vBase = ref MemoryMarshal.GetReference(this.values);
            return Extensions.UnsafeAdd(ref vBase, (row * this.Columns) + column);
        }
    }

    [Conditional("DEBUG")]
    private void CheckCoordinates(uint row, uint column)
    {
        if (row >= this.Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"{row} is outwith the matrix bounds.");
        }

        if (column >= this.Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"{column} is outwith the matrix bounds.");
        }
    }
}
