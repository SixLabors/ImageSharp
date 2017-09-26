// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// An interface that represents a pinned buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal interface IBuffer2D<T>
        where T : struct
    {
        /// <summary>
        /// Gets the width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the backing buffer.
        /// </summary>
        Span<T> Span { get; }
    }
}