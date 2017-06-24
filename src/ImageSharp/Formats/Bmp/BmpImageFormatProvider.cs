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
    /// Detects gif file headers
    /// </summary>
    public class BmpImageFormatProvider : IImageFormatProvider
    {
        /// <inheritdoc/>
        public void Configure(IImageFormatHost host)
        {
            var encoder = new BmpEncoder();
            foreach (string mimeType in BmpConstants.MimeTypes)
            {
                host.SetMimeTypeEncoder(mimeType, encoder);
            }

            foreach (string ext in BmpConstants.FileExtensions)
            {
                foreach (string mimeType in BmpConstants.MimeTypes)
                {
                    host.SetFileExtensionToMimeTypeMapping(ext, mimeType);
                }
            }

            var decoder = new BmpDecoder();
            foreach (string mimeType in BmpConstants.MimeTypes)
            {
                host.SetMimeTypeDecoder(mimeType, decoder);
            }

            host.AddMimeTypeDetector(new BmpMimeTypeDetector());
        }
    }
}
