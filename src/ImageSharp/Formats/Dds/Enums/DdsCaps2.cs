// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Dds.Emums
{
    /// <summary>
    /// Additional detail about the surfaces stored.
    /// </summary>
    /// <remarks>
    /// Although Direct3D 9 supports partial cube-maps, Direct3D 10, 10.1, and 11 require that you
    /// define all six cube-map faces (that is, you must set DDS_Cubemap_ALLFACES).
    /// </remarks>
    [Flags]
    internal enum DdsCaps2 : uint
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Required for a cube map.
        /// </summary>
        Cubemap = 0x200,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapPositiveX = 0x400,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapNegativeX = 0x800,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapPositiveY = 0x1000,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapNegativeY = 0x2000,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapPositiveZ = 0x4000,

        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        CubemapNegativeZ = 0x8000,

        /// <summary>
        /// Required for a volume texture.
        /// </summary>
        Volume = 0x200000
    }
}
