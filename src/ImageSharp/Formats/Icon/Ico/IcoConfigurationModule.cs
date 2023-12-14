// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Ico;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the Ico format.
/// </summary>
public sealed class IcoConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        // TODO: IcoEncoder
        // configuration.ImageFormatsManager.SetEncoder(IcoFormat.Instance, new IcoEncoder());
        configuration.ImageFormatsManager.SetDecoder(IcoFormat.Instance, IcoDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new IconImageFormatDetector());
    }
}
