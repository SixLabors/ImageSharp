// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public abstract class MemoryGroupTestsBase
    {
        internal readonly TestMemoryAllocator MemoryAllocator = new TestMemoryAllocator();

        /// <summary>
        /// Create a group, either uninitialized or filled with incrementing numbers starting with 1.
        /// </summary>
        internal MemoryGroup<int> CreateTestGroup(long totalLength, int bufferLength, bool fillSequence = false)
        {
            this.MemoryAllocator.BufferCapacityInBytes = bufferLength * sizeof(int);
            var g = MemoryGroup<int>.Allocate(this.MemoryAllocator, totalLength, bufferLength);

            if (!fillSequence)
            {
                return g;
            }

            int j = 1;
            for (MemoryGroupIndex i = g.MinIndex(); i < g.MaxIndex(); i += 1)
            {
                g.SetElementAt(i, j);
                j++;
            }

            return g;
        }
    }
}
