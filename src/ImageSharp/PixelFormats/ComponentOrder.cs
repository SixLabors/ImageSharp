// <copyright file="ComponentOrder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    /// <summary>
    /// Enumerates the various component orders.
    /// </summary>
    public enum ComponentOrder
    {
        /// <summary>
        /// Z-> Y-> X order. Equivalent to B-> G-> R in <see cref="Rgba32"/>
        /// </summary>
        Zyx,

        /// <summary>
        /// Z-> Y-> X-> W order. Equivalent to B-> G-> R-> A in <see cref="Rgba32"/>
        /// </summary>
        Zyxw,

        /// <summary>
        /// X-> Y-> Z order. Equivalent to R-> G-> B in <see cref="Rgba32"/>
        /// </summary>
        Xyz,

        /// <summary>
        /// X-> Y-> Z-> W order. Equivalent to R-> G-> B-> A in <see cref="Rgba32"/>
        /// </summary>
        Xyzw,
    }
}
