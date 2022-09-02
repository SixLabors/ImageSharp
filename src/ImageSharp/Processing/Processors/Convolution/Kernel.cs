// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            get
            {
                return this.values;
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int row, int column, T value)
        {
            this.SetValue((row * this.Columns) + column, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int index, T value)
        {
            ref T vBase = ref MemoryMarshal.GetReference(this.values);
            Unsafe.Add(ref vBase, index) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this.values.Clear();
        }

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
    }
}
