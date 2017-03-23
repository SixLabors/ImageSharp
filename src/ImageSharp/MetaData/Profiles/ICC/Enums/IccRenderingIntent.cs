// <copyright file="IccRenderingIntent.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Rendering intent
    /// </summary>
    internal enum IccRenderingIntent : uint
    {
        Perceptual = 0,
        MediaRelativeColorimetric = 1,
        Saturation = 2,
        AbsoluteColorimetric = 3,
    }
}
