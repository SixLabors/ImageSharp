// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
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
        {
            int kernelHeight = kernel.Rows;
            int kernelWidth = kernel.Columns;
            this.yOffsets = this.allocator.Allocate<int>(bounds.Height * kernelHeight);
            this.xOffsets = this.allocator.Allocate<int>(bounds.Width * kernelWidth);

            int minY = bounds.Y;
            int maxY = bounds.Bottom - 1;
            int minX = bounds.X;
            int maxX = bounds.Right - 1;

            int radiusY = kernelHeight >> 1;
            int radiusX = kernelWidth >> 1;

            // Calculate the potential sampling y-offsets.
            Span<int> ySpan = this.yOffsets.GetSpan();
            for (int row = 0; row < bounds.Height; row++)
            {
                for (int y = 0; y < kernelHeight; y++)
                {
                    ySpan[(row * kernelHeight) + y] = row + y + minY - radiusY;
                }
            }

            if (kernelHeight > 1)
            {
                Numerics.Clamp(ySpan, minY, maxY);
            }

            // Calculate the potential sampling x-offsets.
            Span<int> xSpan = this.xOffsets.GetSpan();
            for (int column = 0; column < bounds.Width; column++)
            {
                for (int x = 0; x < kernelWidth; x++)
                {
                    xSpan[(column * kernelWidth) + x] = column + x + minX - radiusX;
                }
            }

            if (kernelWidth > 1)
            {
                Numerics.Clamp(xSpan, minX, maxX);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetYOffsetSpan() => this.yOffsets.GetSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetXOffsetSpan() => this.xOffsets.GetSpan();

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.yOffsets.Dispose();
                this.xOffsets.Dispose();

                this.isDisposed = true;
            }
        }
    }
}
