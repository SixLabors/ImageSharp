// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.PixelFormats
{
    [Flags]
    internal enum PixelConversionModifiers
    {
        None = 0,
        Scale = 1 << 0,
        Premultiply = 1 << 1,
        SRgbCompand = 1 << 2,
    }
}