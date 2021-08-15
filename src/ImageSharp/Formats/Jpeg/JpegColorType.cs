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
        /// Medium Quality - The horizontal sampling is halved and the Cb and Cr channels are only
        /// sampled on each alternate line.
        /// </summary>
        YCbCrRatio420 = 0,

        /// <summary>
        /// YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification.
        /// High Quality - Each of the three Y'CbCr components have the same sample rate,
        /// thus there is no chroma subsampling.
        /// </summary>
        YCbCrRatio444 = 1,

        /// <summary>
        /// Single channel, luminance.
        /// </summary>
        Luminance = 2,

        /// <summary>
        /// The pixel data will be preserved as RGB without any sub sampling.
        /// </summary>
        Rgb = 3,
    }
}
