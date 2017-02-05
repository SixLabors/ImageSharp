// <copyright file="ImageFrameMetaData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Diagnostics;

    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetaData : IMetaData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class.
        /// </summary>
        internal ImageFrameMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class
        /// by making a copy from other metadata.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageFrameMetaData"/> to create this instance from.
        /// </param>
        internal ImageFrameMetaData(ImageFrameMetaData other)
        {
            Debug.Assert(other != null);

            this.FrameDelay = other.FrameDelay;
        }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        public int FrameDelay { get; set; }
    }
}
