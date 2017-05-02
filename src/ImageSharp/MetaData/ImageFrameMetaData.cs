// <copyright file="ImageFrameMetaData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.Formats;

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
            DebugGuard.NotNull(other, nameof(other));

            this.FrameDelay = other.FrameDelay;
            this.DisposalMethod = other.DisposalMethod;
        }

        /// <inheritdoc/>
        public int FrameDelay { get; set; }

        /// <inheritdoc/>
        public DisposalMethod DisposalMethod { get; set; }
    }
}
