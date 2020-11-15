// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Experimental.WebP
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
            configuration.ImageFormatsManager.SetEncoder(WebPFormat.Instance, new WebPEncoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new WebPImageFormatDetector());
        }
    }
}
