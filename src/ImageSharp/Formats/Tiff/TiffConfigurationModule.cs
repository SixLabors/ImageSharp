// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the TIFF format.
    /// </summary>
    public sealed class TiffConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration host)
        {
            host.ImageFormatsManager.SetEncoder(ImageFormats.Tiff, new TiffEncoder());
            host.ImageFormatsManager.SetDecoder(ImageFormats.Tiff, new TiffDecoder());
            host.ImageFormatsManager.AddImageFormatDetector(new TiffImageFormatDetector());
        }
    }
}