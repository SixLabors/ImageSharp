// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// A stack only struct used for reducing reference indirection during 2D convolution operations.
    /// </summary>
    internal readonly ref struct Convolution2DState
    {
        private readonly Span<int> rowOffsetMap;
        private readonly Span<int> columnOffsetMap;
        private readonly int kernelHeight;
        private readonly int kernelWidth;

        public Convolution2DState(
            in DenseMatrix<float> kernelY,
            in DenseMatrix<float> kernelX,
            KernelSamplingMap map)
        {
            // We check the kernels are the same size upstream.
            this.KernelY = new ReadOnlyKernel(kernelY);
            this.KernelX = new ReadOnlyKernel(kernelX);
            this.kernelHeight = kernelY.Rows;
            this.kernelWidth = kernelY.Columns;
            this.rowOffsetMap = map.GetRowOffsetSpan();
            this.columnOffsetMap = map.GetColumnOffsetSpan();
        }

        public readonly ReadOnlyKernel KernelY
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public readonly ReadOnlyKernel KernelX
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
