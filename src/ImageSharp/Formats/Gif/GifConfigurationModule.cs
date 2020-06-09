// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    public sealed class GifConfigurationModule : IConfigurationModule
    {
        /// <inheritdoc/>
        public void Configure(Configuration configuration)
        {
            configuration.ImageFormatsManager.SetEncoder(GifFormat.Instance, new GifEncoder());
            configuration.ImageFormatsManager.SetDecoder(GifFormat.Instance, new GifDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new GifImageFormatDetector());
        }
    }
}