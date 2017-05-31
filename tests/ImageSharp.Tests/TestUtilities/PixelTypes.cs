// <copyright file="PixelTypes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Flags that are mapped to PackedPixel types.
    /// They trigger the desired parametrization for <see cref="TestImageProvider{TPixel}"/>.
    /// </summary>
    [Flags]
    public enum PixelTypes : uint
    {
        Undefined = 0,

        Alpha8 = 1 << 0,

        Argb32 = 1 << 1,

        Bgr565 = 1 << 2,

        Bgra4444 = 1 << 3,

        Byte4 = 1 << 4,

        HalfSingle = 1 << 5,

        HalfVector2 = 1 << 6,

        HalfVector4 = 1 << 7,

        NormalizedByte2 = 1 << 8,

        NormalizedByte4 = 1 << 9,

        NormalizedShort4 = 1 << 10,

        Rg32 = 1 << 11,

        Rgba1010102 = 1 << 12,

        Rgba32 = 1 << 13,

        Rgba64 = 1 << 14,

        RgbaVector = 1 << 15,

        Short2 = 1 << 16,

        Short4 = 1 << 17,

        // TODO: Add multi-flag entries by rules defined in PackedPixelConverterHelper

        // "All" is handled as a separate, individual case instead of using bitwise OR
        All = 30
    }
}