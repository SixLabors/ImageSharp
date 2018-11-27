// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Holds the <see cref="ResizeKernel"/> values in an optimized contigous memory region.
    /// </summary>
    internal class KernelMap : IDisposable
    {
        private readonly IResampler sampler;

        private readonly int sourceSize;

        private readonly float ratio;

        private readonly float scale;

        private readonly int radius;

        private readonly Buffer2D<float> data;

        private readonly ResizeKernel[] kernels;

        private KernelMap(
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
            this.kernels = new ResizeKernel[destinationSize];
        }

        public int DestinationSize { get; }

        /// <summary>
        /// Disposes <see cref="KernelMap"/> instance releasing it's backing buffer.
        /// </summary>
        public void Dispose()
        {
            this.data.Dispose();
        }

        /// <summary>
        /// Returns a <see cref="ResizeKernel"/> for an index value between 0 and destinationSize - 1.
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
        /// <returns>The <see cref="KernelMap"/></returns>
        public static KernelMap Calculate(
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

            int period = ImageMaths.LeastCommonMultiple(sourceSize, destinationSize) / sourceSize;
            int radius = (int)MathF.Ceiling(scale * sampler.Radius);

            var result = new KernelMap(
                memoryAllocator,
                sampler,
                sourceSize,
                destinationSize,
                destinationSize,
                ratio,
                scale,
                radius);

            result.BasicInit();

            return result;
        }

        private void BasicInit()
        {
            for (int i = 0; i < this.DestinationSize; i++)
            {
                float center = ((i + .5F) * this.ratio) - .5F;

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

                ResizeKernel kernel = this.CreateKernel(i, left, right);
                this.kernels[i] = kernel;

                ref float kernelBaseRef = ref MemoryMarshal.GetReference(kernel.GetValues());

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
            }
        }

        /// <summary>
        /// Slices a weights value at the given positions.
        /// </summary>
        private ResizeKernel CreateKernel(int destIdx, int left, int rightIdx)
        {
            int length = rightIdx - left + 1;

            if (length > this.data.Width)
            {
                throw new InvalidOperationException($"Error in KernelMap.CreateKernel({destIdx},{left},{rightIdx}): left > this.data.Width");
            }

            int flatStartIndex = destIdx * this.data.Width;

            Memory<float> bufferSlice = this.data.Memory.Slice(flatStartIndex, length);
            return new ResizeKernel(left, bufferSlice);
        }
    }
}