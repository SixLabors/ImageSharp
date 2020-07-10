// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Provides an <see cref="IManagedByteBuffer"/> based on <see cref="BasicArrayBuffer{T}"/>.
    /// </summary>
    internal sealed class BasicByteBuffer : BasicArrayBuffer<byte>, IManagedByteBuffer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BasicByteBuffer"/> class.
        /// </summary>
        /// <param name="array">The byte array.</param>
        internal BasicByteBuffer(byte[] array)
            : base(array)
        {
        }
    }
}