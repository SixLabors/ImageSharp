// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Registers the image encoder, decoder, and format detector for the ICO format.
/// </summary>
public sealed class IcoConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(IcoFormat.Instance, new IcoEncoder());
        configuration.ImageFormatsManager.SetDecoder(IcoFormat.Instance, IcoDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new IcoImageFormatDetector());
    }
}
