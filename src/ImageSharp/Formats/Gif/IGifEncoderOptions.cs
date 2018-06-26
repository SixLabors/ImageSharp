// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using SixLabors.ImageSharp.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The configuration options used for encoding gifs.
    /// </summary>
    internal interface IGifEncoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the text encoding used to write comments.
        /// </summary>
        Encoding TextEncoding { get; }

        /// <summary>
        /// Gets the quantizer used to generate the color palette.
        /// </summary>
        IQuantizer Quantizer { get; }

        /// <summary>
        /// Gets a value indicating whether pixels that stay the same between two frames should be reused
        /// by forcing the <see cref="DisposalMethod"/> of all frames to <see cref="DisposalMethod.NotDispose"/>
        /// and making the repeated pixel transparent. This improves GIF quality when pixels are
        /// repeated across frames.
        /// </summary>
        bool CutRepeatedPixels { get; }
    }
}