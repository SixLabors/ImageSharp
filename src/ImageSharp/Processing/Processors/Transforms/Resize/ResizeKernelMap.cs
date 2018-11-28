// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides <see cref="ResizeKernel"/> values from an optimized,
    /// contigous memory region.
    /// </summary>
    internal partial class ResizeKernelMap : IDisposable
    {
        private readonly IResampler sampler;

        private readonly int sourceSize;

        private readonly float ratio;

        private readonly float scale;

        private readonly int radius;

        private readonly MemoryHandle pinHandle;

        private readonly Buffer2D<float> data;

        private readonly ResizeKernel[] kernels;

        private ResizeKernelMap(
            MemoryAllocator memoryAllocator,
            IResampler sampler,
            int sourceSize,
            int destinationSize,
            int bufferHeight,
            float ratio,
            float scale,
            int radius)
        {
            this.sampler = sampler;
            this.ratio = ratio;
            this.scale = scale;
            this.radius = radius;
            this.sourceSize = sourceSize;
            this.DestinationSize = destinationSize;
            int maxWidth = (radius * 2) + 1;
            this.data = memoryAllocator.Allocate2D<float>(maxWidth, bufferHeight, AllocationOptions.Clean);
            this.pinHandle = this.data.Memory.Pin();
            this.kernels = new ResizeKernel[destinationSize];
        }

        public int DestinationSize { get; }

        /// <summary>
        /// Disposes <see cref="ResizeKernelMap"/> instance releasing it's backing buffer.
        /// </summary>
        public void Dispose()
        {
            this.pinHandle.Dispose();
            this.data.Dispose();
        }

        /// <summary>
        /// Returns a <see cref="ResizeKernel"/> for an index value between 0 and DestinationSize - 1.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ref ResizeKernel GetKernel(int destIdx) => ref this.kernels[destIdx];

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="sampler">The <see cref="IResampler"/></param>
        /// <param name="destinationSize">The destination size</param>
        /// <param name="sourceSize">The source size</param>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations</param>
        /// <returns>The <see cref="ResizeKernelMap"/></returns>
        public static ResizeKernelMap Calculate(
            IResampler sampler,
            int destinationSize,
            int sourceSize,
            MemoryAllocator memoryAllocator)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            int radius = (int)MathF.Ceiling(scale * sampler.Radius);
            int period = ImageMaths.LeastCommonMultiple(sourceSize, destinationSize) / sourceSize;
            float center0 = (ratio - 1) * 0.5f;
            int cornerInterval = (int)MathF.Ceiling((radius - center0 - 1) / ratio);

            bool useMosaic = 2 * (cornerInterval + period) < destinationSize;

            useMosaic = false;

            ResizeKernelMap result = useMosaic
                                         ? new MosaicKernelMap(
                                             memoryAllocator,
                                             sampler,
                                             sourceSize,
                                             destinationSize,
                                             ratio,
                                             scale,
                                             radius,
                                             period,
                                             cornerInterval)
                                         : new ResizeKernelMap(
                                             memoryAllocator,
                                             sampler,
                                             sourceSize,
                                             destinationSize,
                                             destinationSize,
                                             ratio,
                                             scale,
                                             radius);

            result.Initialize();

            return result;
        }

        protected virtual void Initialize()
        {
            for (int destRowIndex = 0; destRowIndex < this.DestinationSize; destRowIndex++)
            {
                ResizeKernel kernel = this.BuildKernelRow(destRowIndex, destRowIndex);
                this.kernels[destRowIndex] = kernel;
            }
        }

        private ResizeKernel BuildKernelRow(int destRowIndex, int dataRowIndex)
        {
            float center = ((destRowIndex + .5F) * this.ratio) - .5F;

            // Keep inside bounds.
            int left = (int)MathF.Ceiling(center - this.radius);
            if (left < 0)
            {
                left = 0;
            }

            int right = (int)MathF.Floor(center + this.radius);
            if (right > this.sourceSize - 1)
            {
                right = this.sourceSize - 1;
            }

            float sum = 0;

            ResizeKernel kernel = this.GetKernel(dataRowIndex, left, right);

            ref float kernelBaseRef = ref MemoryMarshal.GetReference(kernel.Values);

            for (int j = left; j <= right; j++)
            {
                float value = this.sampler.GetValue((j - center) / this.scale);
                sum += value;

                // weights[j - left] = weight:
                Unsafe.Add(ref kernelBaseRef, j - left) = value;
            }

            // Normalize, best to do it here rather than in the pixel loop later on.
            if (sum > 0)
            {
                for (int w = 0; w < kernel.Length; w++)
                {
                    // weights[w] = weights[w] / sum:
                    ref float kRef = ref Unsafe.Add(ref kernelBaseRef, w);
                    kRef /= sum;
                }
            }

            return kernel;
        }

        /// <summary>
        /// Returns a <see cref="ResizeKernel"/> referencing values of <see cref="data"/>
        /// at row <paramref name="dataRowIndex"/>.
        /// </summary>
        private unsafe ResizeKernel GetKernel(int dataRowIndex, int left, int right)
        {
            int length = right - left + 1;

            if (length > this.data.Width)
            {
                throw new InvalidOperationException(
                    $"Error in KernelMap.CreateKernel({dataRowIndex},{left},{right}): left > this.data.Width");
            }

            Span<float> rowSpan = this.data.GetRowSpan(dataRowIndex);

            ref float rowReference = ref MemoryMarshal.GetReference(rowSpan);
            float* rowPtr = (float*)Unsafe.AsPointer(ref rowReference);
            return new ResizeKernel(left, rowPtr, length);
        }
    }
}