// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Helper methods for the Configuration.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Registers the webp format detector, encoder and decoder.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public static void AddWebp(this Configuration configuration)
        {
            configuration.ImageFormatsManager.AddImageFormat(WebpFormat.Instance);
            configuration.ImageFormatsManager.AddImageFormatDetector(new WebpImageFormatDetector());
            configuration.ImageFormatsManager.SetDecoder(WebpFormat.Instance, new WebpDecoder());
            configuration.ImageFormatsManager.SetEncoder(WebpFormat.Instance, new WebpEncoder());
        }
    }
}
