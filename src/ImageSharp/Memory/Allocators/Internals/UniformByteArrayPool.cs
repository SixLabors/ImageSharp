// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal class UniformByteArrayPool
    {
        private readonly int arraySize;
        private readonly int capacity;

        public UniformByteArrayPool(int arraySize, int capacity)
        {
            this.arraySize = arraySize;
            this.capacity = capacity;
        }

        public bool TryRent(Span<byte[]> result)
        {
            return true;
        }

        public void Return(Span<byte[]> arrays)
        {
        }
    }
}
