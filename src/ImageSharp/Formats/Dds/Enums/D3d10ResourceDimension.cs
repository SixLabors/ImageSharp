// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Dds.Emums
{
    /// <summary>
    /// Identifies the type of resource being used.
    /// </summary>
    internal enum D3d10ResourceDimension : uint
    {
        /// <summary>
        /// Resource is of unknown type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Resource is a buffer.
        /// </summary>
        Buffer = 1,

        /// <summary>
        /// Resource is a 1D Texture.
        /// </summary>
        Texture1D = 2,

        /// <summary>
        /// Resource is a 2D Texture.
        /// </summary>
        Texture2D = 3,

        /// <summary>
        /// Resource is a 3D Texture.
        /// </summary>
        Texture3D = 4
    }
}
