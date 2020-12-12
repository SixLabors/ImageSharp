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
    /// <typeparam name="T">The type of values for the kernel in use.</typeparam>
    internal readonly ref struct ConvolutionState<T>
        where T : unmanaged, IEquatable<T>
    {
        private readonly Span<int> rowOffsetMap;
        private readonly Span<int> columnOffsetMap;
        private readonly int kernelHeight;
        private readonly int kernelWidth;

        public ConvolutionState(
            in DenseMatrix<T> kernel,
            KernelSamplingMap map)
        {
            this.Kernel = new ReadOnlyKernel<T>(kernel);
            this.kernelHeight = kernel.Rows;
            this.kernelWidth = kernel.Columns;
            this.rowOffsetMap = map.GetRowOffsetSpan();
            this.columnOffsetMap = map.GetColumnOffsetSpan();
        }

        public ConvolutionState(
            T[] kernel,
            int height,
            int width,
            KernelSamplingMap map)
        {
            this.Kernel = new ReadOnlyKernel<T>(kernel, height, width);
            this.kernelHeight = height;
            this.kernelWidth = width;
            this.rowOffsetMap = map.GetRowOffsetSpan();
            this.columnOffsetMap = map.GetColumnOffsetSpan();
        }

        public readonly ReadOnlyKernel<T> Kernel
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
