// <copyright file="ComponentOrder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Enumerates the various component orders.
    /// </summary>
    public enum ComponentOrder
    {
        /// <summary>
        /// Blue-> Green-> Red order.
        /// </summary>
        BGR,

        /// <summary>
        /// Blue-> Green-> Red-> Alpha order.
        /// </summary>
        BGRA,

        /// <summary>
        /// Red-> Green-> Blue order.
        /// </summary>
        RGB,

        /// <summary>
        /// Red-> Green-> Blue-> Alpha order.
        /// </summary>
        RGBA,
    }
}
