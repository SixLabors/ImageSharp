// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    internal enum ExrPixelType : int
    {
        /// <summary>
        /// unsigned int (32 bit).
        /// </summary>
        Uint = 0,

        /// <summary>
        /// half (16 bit floating point).
        /// </summary>
        Half = 1,

        /// <summary>
        /// float (32 bit floating point).
        /// </summary>
        Float = 2
    }
}
