// <copyright file="IBuffer2D{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System;

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