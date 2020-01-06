// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Dds.Emums
{
    /// <summary> Specifies the complexity of the surfaces stored. </summary>
    /// <remarks>
    /// The DDS_SURFACE_FLAGS_MIPMAP flag, which is defined in Dds.h, is a bitwise-OR combination of the
    /// <see cref="Complex" /> and  <see cref="MipMap" /> flags.
    /// The DDS_SURFACE_FLAGS_TEXTURE flag, which is defined in Dds.h, is equal to the
    /// <see cref="Texture" /> flag.
    /// The DDS_SURFACE_FLAGS_CUBEMAP flag, which is defined in Dds.h, is equal to the
    /// <see cref="Complex" /> flag.
    /// </remarks>
    [Flags]
    internal enum DdsCaps1 : uint
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Optional; must be used on any file that contains more than one surface
        /// (a mipmap, a cubic environment map, or mipmapped volume texture).
        /// </summary>
        Complex = 0x8,

        /// <summary>
        /// Optional; should be used for a mipmap.
        /// </summary>
        MipMap = 0x400000,

        /// <summary>
        /// Required.
        /// </summary>
        Texture = 0x1000
    }
}
