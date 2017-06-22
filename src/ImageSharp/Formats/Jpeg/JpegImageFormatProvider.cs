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
    /// Detects png file headers
    /// </summary>
    public class JpegImageFormatProvider : IImageFormatProvider
    {
        /// <inheritdoc/>
        public void Configure(IImageFormatHost host)
        {
            var pngEncoder = new JpegEncoder();
            foreach (string mimeType in JpegConstants.MimeTypes)
            {
                host.SetMimeTypeEncoder(mimeType, pngEncoder);
            }

            foreach (string mimeType in JpegConstants.FileExtensions)
            {
                host.SetFileExtensionEncoder(mimeType, pngEncoder);
            }

            var pngDecoder = new JpegDecoder();
            foreach (string mimeType in JpegConstants.MimeTypes)
            {
                host.SetMimeTypeDecoder(mimeType, pngDecoder);
            }

            host.AddMimeTypeDetector(new JpegMimeTypeDetector());
        }
    }
}
