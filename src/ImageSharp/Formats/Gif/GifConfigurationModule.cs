// <copyright file="GifConfigurationModule.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    public sealed class GifConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration config)
        {
            config.SetEncoder(ImageFormats.Gif, new GifEncoder());
            config.SetDecoder(ImageFormats.Gif, new GifDecoder());

            config.AddImageFormatDetector(new GifImageFormatDetector());
        }
    }
}