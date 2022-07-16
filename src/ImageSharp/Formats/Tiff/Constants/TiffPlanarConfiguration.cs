// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing how the components of each pixel are stored the Tiff file-format.
    /// </summary>
    public enum TiffPlanarConfiguration : ushort
    {
        /// <summary>
        /// Chunky format.
        /// The component values for each pixel are stored contiguously.
        /// The order of the components within the pixel is specified by
        /// PhotometricInterpretation. For example, for RGB data, the data is stored as RGBRGBRGB.
        /// </summary>
        Chunky = 1,

        /// <summary>
        /// Planar format.
        /// The components are stored in separate “component planes.” The
        /// values in StripOffsets and StripByteCounts are then arranged as a 2-dimensional
        /// array, with SamplesPerPixel rows and StripsPerImage columns. (All of the columns
        /// for row 0 are stored first, followed by the columns of row 1, and so on.)
        /// PhotometricInterpretation describes the type of data stored in each component
        /// plane. For example, RGB data is stored with the Red components in one component
        /// plane, the Green in another, and the Blue in another.
        /// </summary>
        Planar = 2
    }
}
