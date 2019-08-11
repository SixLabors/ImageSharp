// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the TIFF format.
    /// </summary>
    public sealed class TiffConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetEncoder(TiffFormat.Instance, new TiffEncoder());
            configuration.ImageFormatsManager.SetDecoder(TiffFormat.Instance, new TiffDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new TiffImageFormatDetector());
        }
    }
}