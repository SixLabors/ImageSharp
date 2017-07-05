// <copyright file="TiffConfigurationModule.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the TIFF format.
    /// </summary>
    public sealed class TiffConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration host)
        {
            host.SetEncoder(ImageFormats.Tiff, new TiffEncoder());
            host.SetDecoder(ImageFormats.Tiff, new TiffDecoder());
            host.AddImageFormatDetector(new TiffImageFormatDetector());
        }
    }
}