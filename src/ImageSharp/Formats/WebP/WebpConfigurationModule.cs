// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the webp format.
    /// </summary>
    public sealed class WebPConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetDecoder(WebPFormat.Instance, new WebPDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new WebPImageFormatDetector());
        }
    }
}
