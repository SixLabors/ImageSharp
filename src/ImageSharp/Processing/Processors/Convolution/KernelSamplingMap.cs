// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            => this.BuildSamplingOffsetMap(kernel.Rows, kernel.Columns, bounds, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

        /// <summary>
        /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
        /// </summary>
        /// <param name="kernelHeight">The height (number of rows) of the convolution kernel to use.</param>
        /// <param name="kernelWidth">The width (number of columns) of the convolution kernel to use.</param>
        /// <param name="bounds">The source bounds.</param>
        public void BuildSamplingOffsetMap(int kernelHeight, int kernelWidth, Rectangle bounds)
            => this.BuildSamplingOffsetMap(kernelHeight, kernelWidth, bounds, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

        /// <summary>
        /// Builds a map of the sampling offsets for the kernel clamped by the given bounds.
        /// </summary>
        /// <param name="kernelHeight">The height (number of rows) of the convolution kernel to use.</param>
        /// <param name="kernelWidth">The width (number of columns) of the convolution kernel to use.</param>
        /// <param name="bounds">The source bounds.</param>
        /// <param name="xBorderMode">The wrapping mode on the horizontal borders.</param>
        /// <param name="yBorderMode">The wrapping mode on the vertical borders.</param>
        public void BuildSamplingOffsetMap(int kernelHeight, int kernelWidth, Rectangle bounds, BorderWrappingMode xBorderMode, BorderWrappingMode yBorderMode)
        {
            this.yOffsets = this.allocator.Allocate<int>(bounds.Height * kernelHeight);
            this.xOffsets = this.allocator.Allocate<int>(bounds.Width * kernelWidth);

            int minY = bounds.Y;
            int maxY = bounds.Bottom - 1;
            int minX = bounds.X;
            int maxX = bounds.Right - 1;

            this.BuildOffsets(this.yOffsets, bounds.Height, kernelHeight, minY, maxY, yBorderMode);
            this.BuildOffsets(this.xOffsets, bounds.Width, kernelWidth, minX, maxX, xBorderMode);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BuildOffsets(IMemoryOwner<int> offsets, int boundsSize, int kernelSize, int min, int max, BorderWrappingMode borderMode)
        {
            int radius = kernelSize >> 1;
            Span<int> span = offsets.GetSpan();
            ref int spanBase = ref MemoryMarshal.GetReference(span);
            for (int chunk = 0; chunk < boundsSize; chunk++)
            {
                int chunkBase = chunk * kernelSize;
                for (int i = 0; i < kernelSize; i++)
                {
                    Unsafe.Add(ref spanBase, chunkBase + i) = chunk + i + min - radius;
                }
            }

            this.CorrectBorder(span, kernelSize, min, max, borderMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CorrectBorder(Span<int> span, int kernelSize, int min, int max, BorderWrappingMode borderMode)
        {
            var affectedSize = (kernelSize >> 1) * kernelSize;
            ref int spanBase = ref MemoryMarshal.GetReference(span);
            if (affectedSize > 0)
            {
                switch (borderMode)
                {
                    case BorderWrappingMode.Repeat:
                        Numerics.Clamp(span.Slice(0, affectedSize), min, max);
                        Numerics.Clamp(span.Slice(span.Length - affectedSize), min, max);
                        break;
                    case BorderWrappingMode.Mirror:
                        var min2dec = min + min - 1;
                        for (int i = 0; i < affectedSize; i++)
                        {
                            var value = span[i];
                            if (value < min)
                            {
                                span[i] = min2dec - value;
                            }
                        }

                        var max2inc = max + max + 1;
                        for (int i = span.Length - affectedSize; i < span.Length; i++)
                        {
                            var value = span[i];
                            if (value > max)
                            {
                                span[i] = max2inc - value;
                            }
                        }

                        break;
                    case BorderWrappingMode.Bounce:
                        var min2 = min + min;
                        for (int i = 0; i < affectedSize; i++)
                        {
                            var value = span[i];
                            if (value < min)
                            {
                                span[i] = min2 - value;
                            }
                        }

                        var max2 = max + max;
                        for (int i = span.Length - affectedSize; i < span.Length; i++)
                        {
                            var value = span[i];
                            if (value > max)
                            {
                                span[i] = max2 - value;
                            }
                        }

                        break;
                    case BorderWrappingMode.Wrap:
                        var diff = max - min + 1;
                        for (int i = 0; i < affectedSize; i++)
                        {
                            var value = span[i];
                            if (value < min)
                            {
                                span[i] = diff + value;
                            }
                        }

                        for (int i = span.Length - affectedSize; i < span.Length; i++)
                        {
                            var value = span[i];
                            if (value > max)
                            {
                                span[i] = value - diff;
                            }
                        }

                        break;
                }
            }
        }
    }
}
