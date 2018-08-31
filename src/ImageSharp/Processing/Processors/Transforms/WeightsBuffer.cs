// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Holds the <see cref="WeightsWindow"/> values in an optimized contigous memory region.
    /// </summary>
    internal class WeightsBuffer : IDisposable
    {
        private readonly Buffer2D<float> dataBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WeightsBuffer"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for allocations.</param>
        /// <param name="sourceSize">The size of the source window</param>
        /// <param name="destinationSize">The size of the destination window</param>
        public WeightsBuffer(MemoryAllocator memoryAllocator, int sourceSize, int destinationSize)
        {
            this.dataBuffer = memoryAllocator.Allocate2D<float>(sourceSize, destinationSize, AllocationOptions.Clean);
            this.Weights = new WeightsWindow[destinationSize];
        }

        /// <summary>
        /// Gets the calculated <see cref="Weights"/> values.
        /// </summary>
        public WeightsWindow[] Weights { get; }

        /// <summary>
        /// Disposes <see cref="WeightsBuffer"/> instance releasing it's backing buffer.
        /// </summary>
        public void Dispose()
        {
            this.dataBuffer.Dispose();
        }

        /// <summary>
        /// Slices a weights value at the given positions.
        /// </summary>
        /// <param name="destIdx">The index in destination buffer</param>
        /// <param name="leftIdx">The local left index value</param>
        /// <param name="rightIdx">The local right index value</param>
        /// <returns>The weights</returns>
        public WeightsWindow GetWeightsWindow(int destIdx, int leftIdx, int rightIdx)
        {
            return new WeightsWindow(destIdx, leftIdx, this.dataBuffer, rightIdx - leftIdx + 1);
        }
    }
}