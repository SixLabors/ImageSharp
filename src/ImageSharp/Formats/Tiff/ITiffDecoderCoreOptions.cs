// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder core options interface.
    /// </summary>
    internal interface ITiffDecoderCoreOptions
    {
        /// <summary>
        /// Gets or sets the number of bits for each sample of the pixel format used to encode the image.
        /// </summary>
        uint[] BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the lookup table for RGB palette colored images.
        /// </summary>
        uint[] ColorMap { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation implementation to use when decoding the image.
        /// </summary>
        TiffColorType ColorType { get; set; }

        /// <summary>
        /// Gets or sets the compression implementation to use when decoding the image.
        /// </summary>
        TiffCompressionType CompressionType { get; set; }

        /// <summary>
        /// Gets or sets the planar configuration type to use when decoding the image.
        /// </summary>
        TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation.
        /// </summary>
        TiffPhotometricInterpretation PhotometricInterpretation { get; set; }
    }
}
