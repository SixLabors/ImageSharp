// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Defines the contract for an image format.
    /// </summary>
    public interface IImageFormat
    {
        /// <summary>
        /// Gets the name that describes this image format.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the default mimetype that the image format uses
        /// </summary>
        string DefaultMimeType { get; }

        /// <summary>
        /// Gets all the mimetypes that have been used by this image format.
        /// </summary>
        IEnumerable<string> MimeTypes { get; }

        /// <summary>
        /// Gets the file extensions this image format commonly uses.
        /// </summary>
        IEnumerable<string> FileExtensions { get; }
    }

    /// <summary>
    /// Defines the contract for an image format containing metadata.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    public interface IImageFormat<out TFormatMetadata> : IImageFormat
        where TFormatMetadata : class
    {
        /// <summary>
        /// Creates a default instance of the format metadata.
        /// </summary>
        /// <returns>The <typeparamref name="TFormatMetadata"/>.</returns>
        TFormatMetadata CreateDefaultFormatMetadata();
    }

    /// <summary>
    /// Defines the contract for an image format containing metadata with multiple frames.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    public interface IImageFormat<out TFormatMetadata, out TFormatFrameMetadata> : IImageFormat<TFormatMetadata>
        where TFormatMetadata : class
        where TFormatFrameMetadata : class
    {
        /// <summary>
        /// Creates a default instance of the format frame metadata.
        /// </summary>
        /// <returns>The <typeparamref name="TFormatFrameMetadata"/>.</returns>
        TFormatFrameMetadata CreateDefaultFormatFrameMetadata();
    }
}