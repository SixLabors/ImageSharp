// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Enumerates the chroma subsampling method applied to the image.
    /// </summary>
    public enum JpegSubsample
    {
        /// <summary>
        /// High Quality - Each of the three Y'CbCr components have the same sample rate,
        /// thus there is no chroma subsampling.
        /// </summary>
        Ratio444,

        /// <summary>
        /// Medium Quality - The horizontal sampling is halved and the Cb and Cr channels are only
        /// sampled on each alternate line.
        /// </summary>
        Ratio420
    }
}
