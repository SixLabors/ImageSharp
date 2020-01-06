// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Dds.Emums
{
    /// <summary>
    /// Flags to indicate which members contain valid data.
    /// </summary>
    /// <remarks>
    /// The DDS_HEADER_FLAGS_TEXTURE flag, which is defined in Dds.h, is a bitwise-OR combination of the
    /// <see cref="DdsFlags.Caps" />, <see cref="DdsFlags.Height" />,
    /// <see cref="DdsFlags.Width" />, and <see cref="DdsFlags.PixelFormat" /> flags.
    /// The DDS_HEADER_FLAGS_MIPMAP flag, which is defined in Dds.h, is equal to the
    /// <see cref="DdsFlags.MipMapCount" /> flag.
    /// The DDS_HEADER_FLAGS_VOLUME flag, which is defined in Dds.h, is equal to the
    /// <see cref="DdsFlags.Depth" /> flag.
    /// The DDS_HEADER_FLAGS_PITCH flag, which is defined in Dds.h, is equal to the
    /// <see cref="DdsFlags.Pitch" /> flag.
    /// The DDS_HEADER_FLAGS_LINEARSIZE flag, which is defined in Dds.h, is equal to the
    /// <see cref="DdsFlags.LinearSize" /> flag.
    /// </remarks>
    [Flags]
    internal enum DdsFlags : uint
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Caps = 0x1,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Height = 0x2,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Width = 0x4,

        /// <summary>
        /// Required when pitch is provided for an uncompressed texture.
        /// </summary>
        Pitch = 0x8,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        PixelFormat = 0x1000,

        /// <summary>
        /// Required in a mipmapped texture.
        /// </summary>
        MipMapCount = 0x20000,

        /// <summary>
        /// Required when pitch is provided for a compressed texture.
        /// </summary>
        LinearSize = 0x80000,

        /// <summary>
        /// Required in a depth texture.
        /// </summary>
        Depth = 0x800000
    }
}
