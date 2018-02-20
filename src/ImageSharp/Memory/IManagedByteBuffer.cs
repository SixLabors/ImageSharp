// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a byte buffer backed by a managed array.
    /// </summary>
    internal interface IManagedByteBuffer : IBuffer<byte>
    {
        /// <summary>
        /// Gets the managed array backing this buffer instance.
        /// </summary>
        byte[] Array { get; }
    }
}