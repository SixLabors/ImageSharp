// <copyright file="BmpConfigurationModule.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bmp format.
    /// </summary>
    public sealed class BmpConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.SetEncoder(ImageFormats.Bitmap, new BmpEncoder());
            config.SetDecoder(ImageFormats.Bitmap, new BmpDecoder());
            config.AddImageFormatDetector(new BmpImageFormatDetector());
        }
    }
}