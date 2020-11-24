// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Provides enumeration of the alpha value transparency behavior of a pixel format.
    /// </summary>
    public enum PixelAlphaRepresentation
    {
        /// <summary>
        /// Indicates that the pixel format does not contain an alpha channel.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the transparency behavior is premultiplied.
        /// Each color is first scaled by the alpha value. The alpha value itself is the same
        /// in both straight and premultiplied alpha. Typically, no color channel value is
        /// greater than the alpha channel value.
        /// If a color channel value in a premultiplied format is greater than the alpha
        /// channel, the standard source-over blending math results in an additive blend.
        /// </summary>
        Associated,

        /// <summary>
        /// Indicates that the transparency behavior is not premultiplied.
        /// The alpha channel indicates the transparency of the color.
        /// </summary>
        Unassociated
    }
}
