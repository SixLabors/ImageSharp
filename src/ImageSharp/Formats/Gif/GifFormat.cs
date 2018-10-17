// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    public sealed class GifFormat : IImageFormat<GifMetaData, GifFrameMetaData>
    {
        private GifFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static GifFormat Instance { get; } = new GifFormat();

        /// <inheritdoc/>
        public string Name => "GIF";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/gif";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => GifConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => GifConstants.FileExtensions;

        /// <inheritdoc/>
        public GifMetaData CreateDefaultFormatMetaData() => new GifMetaData();

        /// <inheritdoc/>
        public GifFrameMetaData CreateDefaultFormatFrameMetaData() => new GifFrameMetaData();
    }
}