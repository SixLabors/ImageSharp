// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the thresholding applied to image data defined by the Tiff file-format.
    /// </summary>
    internal enum TiffThresholding
    {
        /// <summary>
        /// No dithering or halftoning.
        /// </summary>
        None = 1,

        /// <summary>
        ///  An ordered dither or halftone technique.
        /// </summary>
        Ordered = 2,

        /// <summary>
        /// A randomized process such as error diffusion.
        /// </summary>
        Random = 3
    }
}
