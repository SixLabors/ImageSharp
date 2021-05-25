// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the webp format.
    /// </summary>
    public sealed class WebpConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetDecoder(WebpFormat.Instance, new WebpDecoder());
            configuration.ImageFormatsManager.SetEncoder(WebpFormat.Instance, new WebpEncoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new WebpImageFormatDetector());
        }
    }
}
