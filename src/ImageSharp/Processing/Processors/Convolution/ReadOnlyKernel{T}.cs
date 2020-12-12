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
    /// <typeparam name="T">The type of items in the kernel.</typeparam>
    internal readonly ref struct ReadOnlyKernel<T>
        where T : unmanaged, IEquatable<T>
    {
        private readonly ReadOnlySpan<T> values;

        public ReadOnlyKernel(DenseMatrix<T> matrix)
        {
            this.Columns = matrix.Columns;
            this.Rows = matrix.Rows;
            this.values = matrix.Span;
        }

        public ReadOnlyKernel(T[] kernel, int height, int width)
        {
            this.Columns = width;
            this.Rows = height;
            this.values = kernel;
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

        public T this[int row, int column]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.CheckCoordinates(row, column);
                ref T vBase = ref MemoryMarshal.GetReference(this.values);
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
