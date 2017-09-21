// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Quantizers;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The configuration options used for encoding gifs
    /// </summary>
    internal interface IGifEncoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the encoding that should be used when writing comments.
        /// </summary>
        Encoding TextEncoding { get; }

        /// <summary>
        /// Gets the size of the color palette to use. For gifs the value ranges from 1 to 256. Leave as zero for default size.
        /// </summary>
        int PaletteSize { get; }

        /// <summary>
        /// Gets the transparency threshold.
        /// </summary>
        byte Threshold { get; }

        /// <summary>
        /// Gets the quantizer for reducing the color count.
        /// </summary>
        IQuantizer Quantizer { get;  }
    }
}
