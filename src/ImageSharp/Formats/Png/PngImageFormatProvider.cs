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
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public class PngImageFormatProvider : IImageFormatProvider
    {
        /// <inheritdoc/>
        public void Configure(IImageFormatHost host)
        {
            var pngEncoder = new PngEncoder();
            foreach (string mimeType in PngConstants.MimeTypes)
            {
                host.SetMimeTypeEncoder(mimeType, pngEncoder);
            }

            foreach (string ext in PngConstants.FileExtensions)
            {
                foreach (string mimeType in PngConstants.MimeTypes)
                {
                    host.SetFileExtensionToMimeTypeMapping(ext, mimeType);
                }
            }

            var pngDecoder = new PngDecoder();
            foreach (string mimeType in PngConstants.MimeTypes)
            {
                host.SetMimeTypeDecoder(mimeType, pngDecoder);
            }

            host.AddMimeTypeDetector(new PngMimeTypeDetector());
        }
    }
}
