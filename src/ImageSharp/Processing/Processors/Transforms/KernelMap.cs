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
        private readonly Buffer2D<float> data;

        private readonly ResizeKernel[] kernels;

        private int period;

        private int radius;

        private int periodicRegionMin;

        private int periodicRegionMax;

        private KernelMap(MemoryAllocator memoryAllocator, int destinationSize, int radius, int period)
        {
            this.DestinationSize = destinationSize;
            this.period = period;
            this.radius = radius;
            this.periodicRegionMin = period + radius;
            this.periodicRegionMax = destinationSize - radius;

            int width = (radius * 2) + 1;
            this.data = memoryAllocator.Allocate2D<float>(width, destinationSize, AllocationOptions.Clean);
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
            var result = new KernelMap(memoryAllocator, destinationSize, radius, period);

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
                result.kernels[i] = ws;

                ref float weightsBaseRef = ref MemoryMarshal.GetReference(ws.GetValues());

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

        private int ReduceIndex(int destIndex)
        {
            return destIndex;
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