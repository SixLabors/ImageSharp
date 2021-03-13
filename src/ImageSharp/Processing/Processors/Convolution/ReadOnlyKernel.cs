// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// A stack only, readonly, kernel matrix that can be indexed without
    /// bounds checks when compiled in release mode.
    /// </summary>
    internal readonly ref struct ReadOnlyKernel
    {
        private readonly ReadOnlySpan<float> values;

        public ReadOnlyKernel(DenseMatrix<float> matrix)
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

        public float this[int row, int column]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.CheckCoordinates(row, column);
                ref float vBase = ref MemoryMarshal.GetReference(this.values);
                return Unsafe.Add(ref vBase, (row * this.Columns) + column);
            }
        }

        [Conditional("DEBUG")]
        private void CheckCoordinates(int row, int column)
        {
            if (row < 0 || row >= this.Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, $"{row} is outwith the matrix bounds.");
            }

            if (column < 0 || column >= this.Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, $"{column} is outwith the matrix bounds.");
            }
        }
    }
}
