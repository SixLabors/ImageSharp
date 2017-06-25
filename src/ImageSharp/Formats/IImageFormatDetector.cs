// <copyright file="IImageFormatDetector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Used for detecting mime types from a file header
    /// </summary>
    public interface IImageFormatDetector
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        int HeaderSize { get; }

        /// <summary>
        /// Detect mimetype
        /// </summary>
        /// <param name="header">The <see cref="T:byte[]"/> containing the file header.</param>
        /// <returns>returns the mime type of detected othersie returns null</returns>
        IImageFormat DetectFormat(ReadOnlySpan<byte> header);
    }
}
