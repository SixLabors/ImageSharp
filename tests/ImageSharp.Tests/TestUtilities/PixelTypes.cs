// <copyright file="PixelTypes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;

    /// <summary>
    /// Flags that are mapped to PackedPixel types. 
    /// They trigger the desired parametrization for <see cref="TestImageFactory{TColor}"/>.
    /// </summary>
    [Flags]
    public enum PixelTypes : uint
    {
        None = 0,

        Alpha8 = 1 << 0,

        Argb = 1 << 1,

        Bgr565 = 1 << 2,

        Bgra4444 = 1 << 3,

        Byte4 = 1 << 4,

        Color = 1 << 5,

        HalfSingle = 1 << 6,

        HalfVector2 = 1 << 7,

        HalfVector4 = 1 << 8,

        NormalizedByte2 = 1 << 9,

        NormalizedByte4 = 1 << 10,

        NormalizedShort4 = 1 << 11,

        Rg32 = 1 << 12,

        Rgba1010102 = 1 << 13,

        Rgba64 = 1 << 14,

        Short2 = 1 << 15,

        Short4 = 1 << 16,

        // TODO: Add multi-flag entries by rules defined in PackedPixelConverterHelper

        // "All" is handled as a separate, individual case instead of using bitwise OR
        All = 30 
    }
}