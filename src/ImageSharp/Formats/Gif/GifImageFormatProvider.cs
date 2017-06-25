// <copyright file="PngImageFormatProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    public class GifImageFormatProvider : IImageFormatProvider
    {
        /// <inheritdoc/>
        public void Configure(IImageFormatHost host)
        {
            var encoder = new GifEncoder();
            foreach (string mimeType in GifConstants.MimeTypes)
            {
                host.SetMimeTypeEncoder(mimeType, encoder);
            }

            foreach (string ext in GifConstants.FileExtensions)
            {
                foreach (string mimeType in GifConstants.MimeTypes)
                {
                    host.SetFileExtensionToMimeTypeMapping(ext, mimeType);
                }
            }

            var decoder = new GifDecoder();
            foreach (string mimeType in GifConstants.MimeTypes)
            {
                host.SetMimeTypeDecoder(mimeType, decoder);
            }

            host.AddMimeTypeDetector(new GifMimeTypeDetector());
        }
    }
}
