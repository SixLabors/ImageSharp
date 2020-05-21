// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public sealed class PngConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetEncoder(PngFormat.Instance, new PngEncoder());
            configuration.ImageFormatsManager.SetDecoder(PngFormat.Instance, new PngDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new PngImageFormatDetector());
        }
    }
}