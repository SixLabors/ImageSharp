// <copyright file="JpegConfigurationModule.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the jpeg format.
    /// </summary>
    public sealed class JpegConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.SetEncoder(ImageFormats.Jpeg, new JpegEncoder());
            config.SetDecoder(ImageFormats.Jpeg, new JpegDecoder());

            config.AddImageFormatDetector(new JpegImageFormatDetector());
        }
    }
}
