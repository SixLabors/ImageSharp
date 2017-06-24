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
    /// Represents an abstract class that can register image encoders, decoders and mime type detectors
    /// </summary>
    public interface IImageFormatProvider
    {
        /// <summary>
        /// Called when loaded so the provider and register its encoders, decodes and mime type detectors into an IImageFormatHost.
        /// </summary>
        /// <param name="host">The host that will retain the encoders, decodes and mime type detectors.</param>
        void Configure(IImageFormatHost host);
    }

    /// <summary>
    /// Represents an abstract class that can have encoders decoders and mimetype detecotrs loaded into.
    /// </summary>
    public interface IImageFormatHost
    {
        /// <summary>
        /// Sets a specific image encoder as the encoder for a specific mimetype
        /// </summary>
        /// <param name="mimeType">the target mimetype</param>
        /// <param name="encoder">the encoder to use</param>
        void SetMimeTypeEncoder(string mimeType, IImageEncoder encoder); // could/should this be an Action<IImageEncoder>???

        /// <summary>
        /// Sets a specific image encoder as the encoder for a specific mimetype
        /// </summary>
        /// <param name="extension">the target mimetype</param>
        /// <param name="mimetype">the mimetype this extenion equates to</param>
        void SetFileExtensionToMimeTypeMapping(string extension, string mimetype);

        /// <summary>
        /// Sets a specific image decoder as the decoder for a specific mimetype
        /// </summary>
        /// <param name="mimeType">the target mimetype</param>
        /// <param name="decoder">the decoder to use</param>
        void SetMimeTypeDecoder(string mimeType, IImageDecoder decoder);

        /// <summary>
        /// Adds a new detector for detecting in mime types
        /// </summary>
        /// <param name="detector">The detector</param>
        void AddMimeTypeDetector(IMimeTypeDetector detector);
    }
}
