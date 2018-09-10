// Copyright (c) Six Labors and contributors.
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

    /// <summary>
    /// Defines the contract for an image format containing metadata.
    /// </summary>
    /// <typeparam name="TFormatMetaData">The type of format metadata.</typeparam>
    public interface IImageFormat<out TFormatMetaData> : IImageFormat
        where TFormatMetaData : class
    {
        /// <summary>
        /// Creates a default instance of the format metadata.
        /// </summary>
        /// <returns>The <typeparamref name="TFormatMetaData"/>.</returns>
        TFormatMetaData CreateDefaultFormatMetaData();
    }

    /// <summary>
    /// Defines the contract for an image format containing metadata with multiple frames.
    /// </summary>
    /// <typeparam name="TFormatMetaData">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetaData">The type of format frame metadata.</typeparam>
    public interface IImageFormat<out TFormatMetaData, out TFormatFrameMetaData> : IImageFormat<TFormatMetaData>
        where TFormatMetaData : class
        where TFormatFrameMetaData : class
    {
        /// <summary>
        /// Creates a default instance of the format frame metadata.
        /// </summary>
        /// <returns>The <typeparamref name="TFormatFrameMetaData"/>.</returns>
        TFormatFrameMetaData CreateDefaultFormatFrameMetaData();
    }
}