// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Enumerates the various component orders.
    /// </summary>
    internal enum ComponentOrder
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
