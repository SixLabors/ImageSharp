// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.Memory
{
    internal sealed class BasicByteBuffer : BasicArrayBuffer<byte>, IManagedByteBuffer
    {
        internal BasicByteBuffer(byte[] array)
            : base(array)
        {
        }
    }
}