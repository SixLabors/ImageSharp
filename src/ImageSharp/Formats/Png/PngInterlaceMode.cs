// <copyright file="PngInterlaceMode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Provides enumeration of available PNG interlace modes.
    /// </summary>
    public enum PngInterlaceMode : byte
    {
        /// <summary>
        /// Non interlaced
        /// </summary>
        None,

        /// <summary>
        /// Adam 7 interlacing.
        /// </summary>
        Adam7
    }
}