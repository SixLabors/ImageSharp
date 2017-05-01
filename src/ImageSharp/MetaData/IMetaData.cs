// <copyright file="IMetaData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.Formats;

    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    internal interface IMetaData
    {
        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, when utilized in Gif animation, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        int FrameDelay { get; set; }

        /// <summary>
        /// Gets or sets the disposal method for animated images.
        /// Primarily used in Gif animation, this field indicates the way in which the graphic is to
        /// be treated after being displayed.
        /// </summary>
        DisposalMethod DisposalMethod { get; set; }
    }
}
