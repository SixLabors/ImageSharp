// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Provides Gif specific metadata information for the image frame.
    /// </summary>
    public class GifFrameMetaData : IImageFormatFrameMetaData
    {
        /// <summary>
        /// Gets or sets the length of the color table for paletted images.
        /// If not 0, then this field indicates the maximum number of colors to use when quantizing the
        /// image frame.
        /// </summary>
        public int ColorTableLength { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, when utilized in Gif animation, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int FrameDelay { get; set; }

        /// <summary>
        /// Gets or sets the disposal method for animated images.
        /// Primarily used in Gif animation, this field indicates the way in which the graphic is to
        /// be treated after being displayed.
        /// </summary>
        public GifDisposalMethod DisposalMethod { get; set; }
    }
}