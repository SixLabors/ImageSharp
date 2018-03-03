// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the jpeg format.
    /// </summary>
    public sealed class JpegConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.ImageFormatsManager.SetEncoder(ImageFormats.Jpeg, new JpegEncoder());
            config.ImageFormatsManager.SetDecoder(ImageFormats.Jpeg, new JpegDecoder());

            config.ImageFormatsManager.AddImageFormatDetector(new JpegImageFormatDetector());
        }
    }
}
