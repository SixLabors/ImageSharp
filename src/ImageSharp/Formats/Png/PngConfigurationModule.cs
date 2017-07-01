// <copyright file="PngConfigurationModule.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public sealed class PngConfigurationModule : IConfigurationModule
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