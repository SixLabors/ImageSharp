// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Provides enumeration of available JPEG color types.
    /// </summary>
    public enum JpegColorType : byte
    {
        /// <summary>
        /// YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification.
        /// </summary>
        YCbCr = 0,

        /// <summary>
        /// Single channel, luminance.
        /// </summary>
        Luminance = 1
    }
}
