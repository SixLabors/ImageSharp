// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// A buffer implementation that consumes an existing <see cref="Memory{T}"/> instance.
    /// The ownership of the memory remains external.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    internal sealed class ConsumedBuffer<T> : IBuffer<T>
        where T : struct
    {
        public ConsumedBuffer(Memory<T> memory)
        {
            this.Memory = memory;
        }

        public Memory<T> Memory { get; }

        public Span<T> GetSpan()
        {
            return this.Memory.Span;
        }

        public void Dispose()
        {
        }
    }
}