// <copyright file="IImageFormatProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents an interface that can register image encoders, decoders and mime type detectors.
    /// </summary>
    public interface IImageFormatProvider
    {
        /// <summary>
        /// Called when loaded so the provider and register its encoders, decoders and mime type detectors into an IImageFormatHost.
        /// </summary>
        /// <param name="host">The host that will retain the encoders, decodes and mime type detectors.</param>
        void Configure(IImageFormatHost host);
    }

    /// <summary>
    /// Represents an interface that can have encoders, decoders and mime type detectors loaded into.
    /// </summary>
    public interface IImageFormatHost
    {
        /// <summary>
        /// Sets a specific image encoder as the encoder for a specific mime type.
        /// </summary>
        /// <param name="mimeType">the target mimetype</param>
        /// <param name="encoder">the encoder to use</param>
        void SetMimeTypeEncoder(string mimeType, IImageEncoder encoder); // could/should this be an Action<IImageEncoder>???

        /// <summary>
        /// Sets a mapping value between a file extension and a mime type.
        /// </summary>
        /// <param name="extension">The target mime type</param>
        /// <param name="mimetype">The mime type this extension equates to</param>
        void SetFileExtensionToMimeTypeMapping(string extension, string mimetype);

        /// <summary>
        /// Sets a specific image decoder as the decoder for a specific mime type.
        /// </summary>
        /// <param name="mimeType">The target mime type</param>
        /// <param name="decoder">The decoder to use</param>
        void SetMimeTypeDecoder(string mimeType, IImageDecoder decoder);

        /// <summary>
        /// Adds a new detector for detecting mime types.
        /// </summary>
        /// <param name="detector">The detector to add</param>
        void AddMimeTypeDetector(IMimeTypeDetector detector);
    }
}
