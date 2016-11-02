// <copyright file="ComponentOrder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Enumerates the various component orders.
    /// </summary>
    public enum ComponentOrder
    {
        /// <summary>
        /// Z-> Y-> X order. Equivalent to B-> G-> R in <see cref="Color"/>
        /// </summary>
        ZYX,

        /// <summary>
        /// Z-> Y-> X-> W order. Equivalent to B-> G-> R-> A in <see cref="Color"/>
        /// </summary>
        ZYXW,

        /// <summary>
        /// X-> Y-> Z order. Equivalent to R-> G-> B in <see cref="Color"/>
        /// </summary>
        XYZ,

        /// <summary>
        /// X-> Y-> Z-> W order. Equivalent to R-> G-> B-> A in <see cref="Color"/>
        /// </summary>
        XYZW,
    }
}
