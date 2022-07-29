// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Configuration options for use during PBM encoding.
    /// </summary>
    internal interface IPbmEncoderOptions
    {
        /// <summary>
        /// Gets the encoding of the pixels.
        /// </summary>
        PbmEncoding? Encoding { get; }

        /// <summary>
        /// Gets the Color type of the resulting image.
        /// </summary>
        PbmColorType? ColorType { get; }

        /// <summary>
        /// Gets the Data Type of the pixel components.
        /// </summary>
        PbmComponentType? ComponentType { get; }
    }
}
