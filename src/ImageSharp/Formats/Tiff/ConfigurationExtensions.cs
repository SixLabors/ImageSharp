// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Helper methods for the Configuration.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Registers the tiff format detector, encoder and decoder.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public static void AddTiff(this Configuration configuration)
        {
            configuration.ImageFormatsManager.AddImageFormat(TiffFormat.Instance);
            configuration.ImageFormatsManager.AddImageFormatDetector(new TiffImageFormatDetector());
            configuration.ImageFormatsManager.SetDecoder(TiffFormat.Instance, new TiffDecoder());
            configuration.ImageFormatsManager.SetEncoder(TiffFormat.Instance, new TiffEncoder());
        }
    }
}
