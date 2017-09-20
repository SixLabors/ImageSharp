﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Gif
{
    using System.Collections.Generic;

    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    internal sealed class GifFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Name => "GIF";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/gif";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => GifConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => GifConstants.FileExtensions;
    }
}