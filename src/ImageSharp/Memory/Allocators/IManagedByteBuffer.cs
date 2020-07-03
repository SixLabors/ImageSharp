// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a byte buffer backed by a managed array. Useful for interop with classic .NET API-s.
    /// </summary>
    public interface IManagedByteBuffer : IMemoryOwner<byte>
    {
        /// <summary>
        /// Gets the managed array backing this buffer instance.
        /// </summary>
        byte[] Array { get; }
    }
}