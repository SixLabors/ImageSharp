// <copyright file="PngConfigurationModule.cs" company="James Jackson-South">
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
    public class PngConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration host)
        {
            host.SetEncoder(ImageFormats.Png, new PngEncoder());
            host.SetDecoder(ImageFormats.Png, new PngDecoder());
            host.AddImageFormatDetector(new PngImageFormatDetector());
        }
    }
}
