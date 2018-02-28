// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public sealed class PngConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.SetEncoder(ImageFormats.Png, new PngEncoder());
            config.SetDecoder(ImageFormats.Png, new PngDecoder());
            config.AddImageFormatDetector(new PngImageFormatDetector());
        }
    }
}