// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides resize kernel values from an optimized contiguous memory region.
    /// </summary>
    internal partial class ResizeKernelMap : IDisposable
    {
        private static readonly TolerantMath TolerantMath = TolerantMath.Default;

        private readonly int sourceLength;

        private readonly double ratio;

        private readonly double scale;

        private readonly int radius;

        private readonly MemoryHandle pinHandle;

        private readonly Buffer2D<float> data;

        private readonly ResizeKernel[] kernels;

        private bool isDisposed;

        // To avoid both GC allocations, and MemoryAllocator ceremony:
        private readonly double[] tempValues;

        private ResizeKernelMap(
            MemoryAllocator memoryAllocator,
            int sourceLength,
            int destinationLength,
            int bufferHeight,
            double ratio,
            double scale,
            int radius)
        {
            this.ratio = ratio;
            this.scale = scale;
            this.radius = radius;
            this.sourceLength = sourceLength;
            this.DestinationLength = destinationLength;
            this.MaxDiameter = (radius * 2) + 1;
            this.data = memoryAllocator.Allocate2D<float>(this.MaxDiameter, bufferHeight, AllocationOptions.Clean);
            this.pinHandle = this.data.GetSingleMemory().Pin();
            this.kernels = new ResizeKernel[destinationLength];
            this.tempValues = new double[this.MaxDiameter];
        }

        /// <summary>
        /// Gets the length of the destination row/column
        /// </summary>
        public int DestinationLength { get; }

        /// <summary>
        /// Gets the maximum diameter of the kernels.
        /// </summary>
        public int MaxDiameter { get; }

        /// <summary>
        /// Gets a string of information to help debugging
        /// </summary>
        internal virtual string Info =>
            $"radius:{this.radius}|sourceSize:{this.sourceLength}|destinationSize:{this.DestinationLength}|ratio:{this.ratio}|scale:{this.scale}";

        /// <summary>
        /// Disposes <see cref="ResizeKernelMap"/> instance releasing it's backing buffer.
        /// </summary>
        public void Dispose()
            => this.Dispose(true);

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">Whether to dispose of managed and unmanaged objects.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;

                if (disposing)
                {
                    this.pinHandle.Dispose();
                    this.data.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="ResizeKernel"/> for an index value between 0 and DestinationSize - 1.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal ref ResizeKernel GetKernel(int destIdx) => ref this.kernels[destIdx];

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <typeparam name="TResampler">The type of sampler.</typeparam>
        /// <param name="sampler">The <see cref="IResampler"/></param>
        /// <param name="destinationSize">The destination size</param>
        /// <param name="sourceSize">The source size</param>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations</param>
        /// <returns>The <see cref="ResizeKernelMap"/></returns>
        public static ResizeKernelMap Calculate<TResampler>(
            in TResampler sampler,
            int destinationSize,
            int sourceSize,
            MemoryAllocator memoryAllocator)
            where TResampler : struct, IResampler
        {
            double ratio = (double)sourceSize / destinationSize;
            double scale = ratio;

            if (scale < 1)
            {
                scale = 1;
            }

            int radius = (int)TolerantMath.Ceiling(scale * sampler.Radius);

            // 'ratio' is a rational number.
            // Multiplying it by LCM(sourceSize, destSize)/sourceSize will result in a whole number "again".
            // This value is determining the length of the periods in repeating kernel map rows.
            int period = Numerics.LeastCommonMultiple(sourceSize, destinationSize) / sourceSize;

            // the center position at i == 0:
            double center0 = (ratio - 1) * 0.5;
            double firstNonNegativeLeftVal = (radius - center0 - 1) / ratio;

            // The number of rows building a "stairway" at the top and the bottom of the kernel map
            // corresponding to the corners of the image.
            // If we do not normalize the kernel values, these rows also fit the periodic logic,
            // however, it's just simpler to calculate them separately.
            int cornerInterval = (int)TolerantMath.Ceiling(firstNonNegativeLeftVal);

            // If firstNonNegativeLeftVal was an integral value, we need firstNonNegativeLeftVal+1
            // instead of Ceiling:
            if (TolerantMath.AreEqual(firstNonNegativeLeftVal, cornerInterval))
            {
                cornerInterval++;
            }

            // If 'cornerInterval' is too big compared to 'period', we can't apply the periodic optimization.
            // If we don't have at least 2 periods, we go with the basic implementation:
            bool hasAtLeast2Periods = 2 * (cornerInterval + period) < destinationSize;

            ResizeKernelMap result = hasAtLeast2Periods
                                         ? new PeriodicKernelMap(
                                             memoryAllocator,
                                             sourceSize,
                                             destinationSize,
                                             ratio,
                                             scale,
                                             radius,
                                             period,
                                             cornerInterval)
                                         : new ResizeKernelMap(
                                             memoryAllocator,
                                             sourceSize,
                                             destinationSize,
                                             destinationSize,
                                             ratio,
                                             scale,
                                             radius);

            result.Initialize(in sampler);

            return result;
        }

        /// <summary>
        /// Initializes the kernel map.
        /// </summary>
        protected internal virtual void Initialize<TResampler>(in TResampler sampler)
            where TResampler : struct, IResampler
        {
            for (int i = 0; i < this.DestinationLength; i++)
            {
                this.kernels[i] = this.BuildKernel(in sampler, i, i);
            }
        }

        /// <summary>
        /// Builds a <see cref="ResizeKernel"/> for the row <paramref name="destRowIndex"/> (in <see cref="kernels"/>)
        /// referencing the data at row <paramref name="dataRowIndex"/> within <see cref="data"/>,
        /// so the data reusable by other data rows.
        /// </summary>
        private ResizeKernel BuildKernel<TResampler>(in TResampler sampler, int destRowIndex, int dataRowIndex)
            where TResampler : struct, IResampler
        {
            double center = ((destRowIndex + .5) * this.ratio) - .5;

            // Keep inside bounds.
            int left = (int)TolerantMath.Ceiling(center - this.radius);
            if (left < 0)
            {
                left = 0;
            }

            int right = (int)TolerantMath.Floor(center + this.radius);
            if (right > this.sourceLength - 1)
            {
                right = this.sourceLength - 1;
            }

            ResizeKernel kernel = this.CreateKernel(dataRowIndex, left, right);

            Span<double> kernelValues = this.tempValues.AsSpan().Slice(0, kernel.Length);
            double sum = 0;

            for (int j = left; j <= right; j++)
            {
                double value = sampler.GetValue((float)((j - center) / this.scale));
                sum += value;

                kernelValues[j - left] = value;
            }

            // Normalize, best to do it here rather than in the pixel loop later on.
            if (sum > 0)
            {
                for (int j = 0; j < kernel.Length; j++)
                {
                    // weights[w] = weights[w] / sum:
                    ref double kRef = ref kernelValues[j];
                    kRef /= sum;
                }
            }

            kernel.Fill(kernelValues);

            return kernel;
        }

        /// <summary>
        /// Returns a <see cref="ResizeKernel"/> referencing values of <see cref="data"/>
        /// at row <paramref name="dataRowIndex"/>.
        /// </summary>
        private unsafe ResizeKernel CreateKernel(int dataRowIndex, int left, int right)
        {
            int length = right - left + 1;
            this.ValidateSizesForCreateKernel(length, dataRowIndex, left, right);

            Span<float> rowSpan = this.data.GetRowSpan(dataRowIndex);

            ref float rowReference = ref MemoryMarshal.GetReference(rowSpan);
            float* rowPtr = (float*)Unsafe.AsPointer(ref rowReference);
            return new ResizeKernel(left, rowPtr, length);
        }

        [Conditional("DEBUG")]
        private void ValidateSizesForCreateKernel(int length, int dataRowIndex, int left, int right)
        {
            if (length > this.data.Width)
            {
                throw new InvalidOperationException(
                    $"Error in KernelMap.CreateKernel({dataRowIndex},{left},{right}): left > this.data.Width");
            }
        }
    }
}
