// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bmp format.
    /// </summary>
    public sealed class BmpConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.SetEncoder(ImageFormats.Bmp, new BmpEncoder());
            config.SetDecoder(ImageFormats.Bmp, new BmpDecoder());
            config.AddImageFormatDetector(new BmpImageFormatDetector());
        }
    }
}