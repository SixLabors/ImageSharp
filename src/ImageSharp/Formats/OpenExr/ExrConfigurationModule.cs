// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the OpenExr format.
/// </summary>
public sealed class ExrConfigurationModule : IConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(ExrFormat.Instance, new ExrEncoder());
        configuration.ImageFormatsManager.SetDecoder(ExrFormat.Instance, new ExrDecoder());
        configuration.ImageFormatsManager.AddImageFormatDetector(new ExrImageFormatDetector());
    }
}
