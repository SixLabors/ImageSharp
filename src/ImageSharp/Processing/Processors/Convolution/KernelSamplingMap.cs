// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Provides a map of the convolution kernel sampling offsets.
    /// </summary>
    internal sealed class KernelSamplingMap : IDisposable
    {
        private readonly MemoryAllocator allocator;
        private bool isDisposed;
        private IMemoryOwner<int> yOffsets;
        private IMemoryOwner<int> xOffsets;

        /// <summary>
        /// Initializes a new instance of the <see cref="KernelSamplingMap"/> class.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        public KernelSamplingMap(MemoryAllocator allocator) => this.allocator = allocator;

        /// <summary>
        /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
        /// </summary>
        /// <param name="kernel">The convolution kernel.</param>
        /// <param name="bounds">The source bounds.</param>
        public void BuildSamplingOffsetMap(DenseMatrix<float> kernel, Rectangle bounds)
            => this.BuildSamplingOffsetMap(kernel.Rows, kernel.Columns, bounds);

        /// <summary>
        /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
        /// </summary>
        /// <param name="kernelHeight">The height (number of rows) of the convolution kernel to use.</param>
        /// <param name="kernelWidth">The width (number of columns) of the convolution kernel to use.</param>
        /// <param name="bounds">The source bounds.</param>
        public void BuildSamplingOffsetMap(int kernelHeight, int kernelWidth, Rectangle bounds)
        {
            this.yOffsets = this.allocator.Allocate<int>(bounds.Height * kernelHeight);
            this.xOffsets = this.allocator.Allocate<int>(bounds.Width * kernelWidth);

            int minY = bounds.Y;
            int maxY = bounds.Bottom - 1;
            int minX = bounds.X;
            int maxX = bounds.Right - 1;

            int radiusY = kernelHeight >> 1;
            int radiusX = kernelWidth >> 1;

            // Calculate the y and x sampling offsets clamped to the given rectangle.
            // While this isn't a hotpath we still dip into unsafe to avoid the span bounds
            // checks as the can potentially be looping over large arrays.
            Span<int> ySpan = this.yOffsets.GetSpan();
            ref int ySpanBase = ref MemoryMarshal.GetReference(ySpan);
            for (int row = 0; row < bounds.Height; row++)
            {
                int rowBase = row * kernelHeight;
                for (int y = 0; y < kernelHeight; y++)
                {
                    Unsafe.Add(ref ySpanBase, rowBase + y) = row + y + minY - radiusY;
                }
            }

            if (kernelHeight > 1)
            {
                Numerics.Clamp(ySpan, minY, maxY);
            }

            Span<int> xSpan = this.xOffsets.GetSpan();
            ref int xSpanBase = ref MemoryMarshal.GetReference(xSpan);
            for (int column = 0; column < bounds.Width; column++)
            {
                int columnBase = column * kernelWidth;
                for (int x = 0; x < kernelWidth; x++)
                {
                    Unsafe.Add(ref xSpanBase, columnBase + x) = column + x + minX - radiusX;
                }
            }

            if (kernelWidth > 1)
            {
                Numerics.Clamp(xSpan, minX, maxX);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetRowOffsetSpan() => this.yOffsets.GetSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetColumnOffsetSpan() => this.xOffsets.GetSpan();

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.yOffsets?.Dispose();
                this.xOffsets?.Dispose();

                this.isDisposed = true;
            }
        }
    }
}
