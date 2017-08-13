// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Describes an image format.
    /// </summary>
    public interface IImageFormat
    {
        /// <summary>
        /// Gets the name that describes this image format.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the default mimetype that the image foramt uses
        /// </summary>
        string DefaultMimeType { get; }

        /// <summary>
        /// Gets all the mimetypes that have been used by this image foramt.
        /// </summary>
        IEnumerable<string> MimeTypes { get; }

        /// <summary>
        /// Gets the file extensions this image format commonly uses.
        /// </summary>
        IEnumerable<string> FileExtensions { get; }
    }
}