// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Implements <see cref="MemoryAllocator"/> by newing up managed arrays on every allocation request.
    /// </summary>
    public sealed class SimpleGcMemoryAllocator : MemoryAllocator
    {
        /// <inheritdoc />
        protected internal override int GetBufferCapacityInBytes() => int.MaxValue;

        /// <inheritdoc />
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 0, nameof(length));

            return new BasicArrayBuffer<T>(new T[length]);
        }
    }
}
