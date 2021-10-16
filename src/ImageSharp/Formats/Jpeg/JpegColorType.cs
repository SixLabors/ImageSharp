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
        /// YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification.
        /// The two chroma components are sampled at half the horizontal sample rate of luma while vertically it has full resolution.
        ///
        /// Note: Not supported by the encoder.
        /// </summary>
        YCbCrRatio422 = 2,

        /// <summary>
        /// YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification.
        /// In 4:1:1 chroma subsampling, the horizontal color resolution is quartered.
        ///
        /// Note: Not supported by the encoder.
        /// </summary>
        YCbCrRatio411 = 3,

        /// <summary>
        /// YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification.
        /// This ratio uses half of the vertical and one-fourth the horizontal color resolutions.
        ///
        /// Note: Not supported by the encoder.
        /// </summary>
        YCbCrRatio410 = 4,

        /// <summary>
        /// Single channel, luminance.
        /// </summary>
        Luminance = 5,

        /// <summary>
        /// The pixel data will be preserved as RGB without any sub sampling.
        /// </summary>
        Rgb = 6,

        /// <summary>
        /// CMYK colorspace (cyan, magenta, yellow, and key black) intended for printing.
        ///
        /// Note: Not supported by the encoder.
        /// </summary>
        Cmyk = 7,
    }
}
