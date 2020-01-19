// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    // TODO from dsp.h
    public enum WebPFilterType
    {
        None = 0,
        Horizontal,
        Vertical,
        Gradient,
        Last = Gradient + 1, // end marker
        Best, // meta types
        Fast
    }
}
