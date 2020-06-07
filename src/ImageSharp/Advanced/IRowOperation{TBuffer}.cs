// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row with a temporary buffer.
    /// </summary>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    public interface IRowOperation<TBuffer>
        where TBuffer : unmanaged
    {
        /// <summary>
        /// Invokes the method passing the row and a buffer.
        /// </summary>
        /// <param name="y">The row y coordinate.</param>
        /// <param name="span">The contiguous region of memory.</param>
        void Invoke(int y, Span<TBuffer> span);
    }
}
