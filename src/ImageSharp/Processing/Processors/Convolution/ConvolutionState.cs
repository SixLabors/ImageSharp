// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// A stack only struct used for reducing reference indirection during convolution operations.
    /// </summary>
    internal readonly ref struct ConvolutionState
    {
        private readonly Span<int> rowOffsetMap;
        private readonly Span<int> columnOffsetMap;
        private readonly int kernelHeight;
        private readonly int kernelWidth;

        public ConvolutionState(
            in DenseMatrix<float> kernel,
            KernelSamplingMap map)
        {
            this.Kernel = new ReadOnlyKernel(kernel);
            this.kernelHeight = kernel.Rows;
            this.kernelWidth = kernel.Columns;
            this.rowOffsetMap = map.GetRowOffsetSpan();
            this.columnOffsetMap = map.GetColumnOffsetSpan();
        }

        public readonly ReadOnlyKernel Kernel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref int GetSampleRow(int row)
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.rowOffsetMap), row * this.kernelHeight);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref int GetSampleColumn(int column)
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.columnOffsetMap), column * this.kernelWidth);
    }
}
