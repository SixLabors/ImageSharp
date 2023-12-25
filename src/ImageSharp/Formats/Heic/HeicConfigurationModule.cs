// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the HEIC format.
/// </summary>
public sealed class HeicConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(HeicFormat.Instance, new HeicEncoder());
        configuration.ImageFormatsManager.SetDecoder(HeicFormat.Instance, HeicDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new HeicImageFormatDetector());
    }
}
