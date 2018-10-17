// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Holds the <see cref="ResizeKernel"/> values in an optimized contigous memory region.
    /// </summary>
    internal class KernelMap : IDisposable
    {
        private readonly Buffer2D<float> data;

        /// <summary>
        /// Initializes a new instance of the <see cref="KernelMap"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for allocations.</param>
        /// <param name="destinationSize">The size of the destination window</param>
        /// <param name="kernelRadius">The radius of the kernel</param>
        public KernelMap(MemoryAllocator memoryAllocator, int destinationSize, float kernelRadius)
        {
            int width = (int)Math.Ceiling(kernelRadius * 2);
            this.data = memoryAllocator.Allocate2D<float>(width, destinationSize, AllocationOptions.Clean);
            this.Kernels = new ResizeKernel[destinationSize];
        }

        /// <summary>
        /// Gets the calculated <see cref="Kernels"/> values.
        /// </summary>
        public ResizeKernel[] Kernels { get; }

        /// <summary>
        /// Disposes <see cref="KernelMap"/> instance releasing it's backing buffer.
        /// </summary>
        public void Dispose()
        {
            this.data.Dispose();
        }

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

            float radius = MathF.Ceiling(scale * sampler.Radius);
            var result = new KernelMap(memoryAllocator, destinationSize, radius);

            for (int i = 0; i < destinationSize; i++)
            {
                float center = ((i + .5F) * ratio) - .5F;

                // Keep inside bounds.
                int left = (int)MathF.Ceiling(center - radius);
                if (left < 0)
                {
                    left = 0;
                }

                int right = (int)MathF.Floor(center + radius);
                if (right > sourceSize - 1)
                {
                    right = sourceSize - 1;
                }

                float sum = 0;

                ResizeKernel ws = result.CreateKernel(i, left, right);
                result.Kernels[i] = ws;

                ref float weightsBaseRef = ref ws.GetStartReference();

                for (int j = left; j <= right; j++)
                {
                    float weight = sampler.GetValue((j - center) / scale);
                    sum += weight;

                    // weights[j - left] = weight:
                    Unsafe.Add(ref weightsBaseRef, j - left) = weight;
                }

                // Normalize, best to do it here rather than in the pixel loop later on.
                if (sum > 0)
                {
                    for (int w = 0; w < ws.Length; w++)
                    {
                        // weights[w] = weights[w] / sum:
                        ref float wRef = ref Unsafe.Add(ref weightsBaseRef, w);
                        wRef /= sum;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Slices a weights value at the given positions.
        /// </summary>
        /// <param name="destIdx">The index in destination buffer</param>
        /// <param name="leftIdx">The local left index value</param>
        /// <param name="rightIdx">The local right index value</param>
        /// <returns>The weights</returns>
        private ResizeKernel CreateKernel(int destIdx, int leftIdx, int rightIdx)
        {
            return new ResizeKernel(destIdx, leftIdx, this.data, rightIdx - leftIdx + 1);
        }
    }
}