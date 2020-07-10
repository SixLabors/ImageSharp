// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for an action that operates on a row interval with a temporary buffer.
    /// </summary>
    /// <typeparam name="TBuffer">The type of buffer elements.</typeparam>
    public interface IRowIntervalOperation<TBuffer>
        where TBuffer : unmanaged
    {
        /// <summary>
        /// Invokes the method passing the row interval and a buffer.
        /// </summary>
        /// <param name="rows">The row interval.</param>
        /// <param name="span">The contiguous region of memory.</param>
        void Invoke(in RowInterval rows, Span<TBuffer> span);
    }
}
