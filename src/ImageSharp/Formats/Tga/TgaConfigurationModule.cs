// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the tga format.
    /// </summary>
    public sealed class TgaConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetEncoder(TgaFormat.Instance, new TgaEncoder());
            configuration.ImageFormatsManager.SetDecoder(TgaFormat.Instance, new TgaDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new TgaImageFormatDetector());
        }
    }
}
