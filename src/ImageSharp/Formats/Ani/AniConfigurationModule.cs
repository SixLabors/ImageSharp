// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the Ico format.
/// </summary>
public sealed class AniConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        // configuration.ImageFormatsManager.SetEncoder(AniFormat.Instance, new AniEncoder());
        configuration.ImageFormatsManager.SetDecoder(AniFormat.Instance, AniDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new AniImageFormatDetector());
    }
}
