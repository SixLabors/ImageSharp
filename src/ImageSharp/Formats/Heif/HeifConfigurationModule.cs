// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Configures HEIF image-format support.
/// </summary>
public sealed class HeifConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetDecoder(HeifFormat.Instance, HeifDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new HeifImageFormatDetector());
    }
}
