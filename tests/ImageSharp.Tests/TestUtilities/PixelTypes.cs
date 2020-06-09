// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Flags that are mapped to PackedPixel types.
    /// They trigger the desired parametrization for <see cref="TestImageProvider{TPixel}"/>.
    /// </summary>
    [Flags]
    public enum PixelTypes
    {
#pragma warning disable SA1602 // Enumeration items should be documented
        Undefined = 0,

        A8 = 1 << 0,

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

        Rgb24 = 1 << 18,

        Bgr24 = 1 << 19,

        Bgra32 = 1 << 20,

        Rgb48 = 1 << 21,

        Bgra5551 = 1 << 22,

        L8 = 1 << 23,

        L16 = 1 << 24,

        La16 = 1 << 25,

        La32 = 1 << 26,

        // TODO: Add multi-flag entries by rules defined in PackedPixelConverterHelper

        // "All" is handled as a separate, individual case instead of using bitwise OR
        All = 30

#pragma warning restore SA1602 // Enumeration items should be documented
    }
}
