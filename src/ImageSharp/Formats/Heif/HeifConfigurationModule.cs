// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the HEIF format.
/// </summary>
public sealed class HeifConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(HeifFormat.Instance, new HeifEncoder());
        configuration.ImageFormatsManager.SetDecoder(HeifFormat.Instance, HeifDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new HeifImageFormatDetector());
    }
}
