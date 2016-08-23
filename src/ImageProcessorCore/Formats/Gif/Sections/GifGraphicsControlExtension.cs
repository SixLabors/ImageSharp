// <copyright file="GifGraphicsControlExtension.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// The Graphic Control Extension contains parameters used when
    /// processing a graphic rendering block.
    /// </summary>
    internal sealed class GifGraphicsControlExtension
    {
        /// <summary>
        /// Gets or sets the disposal method which indicates the way in which the
        /// graphic is to be treated after being displayed.
        /// </summary>
        public DisposalMethod DisposalMethod { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether transparency flag is to be set.
        /// This indicates whether a transparency index is given in the Transparent Index field.
        /// (This field is the least significant bit of the byte.)
        /// </summary>
        public bool TransparencyFlag { get; set; }

        /// <summary>
        /// Gets or sets the transparency index.
        /// The Transparency Index is such that when encountered, the corresponding pixel
        /// of the display device is not modified and processing goes on to the nexTColor pixel.
        /// </summary>
        public int TransparencyIndex { get; set; }

        /// <summary>
        /// Gets or sets the delay time.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int DelayTime { get; set; }
    }
}
