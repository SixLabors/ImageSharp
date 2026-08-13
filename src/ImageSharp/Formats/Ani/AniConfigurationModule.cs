// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Registers the image encoder, decoder, and format detector for the ANI format.
/// </summary>
public sealed class AniConfigurationModule : IImageFormatConfigurationModule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AniConfigurationModule"/> class.
    /// </summary>
    public AniConfigurationModule()
    {
    }

    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(AniFormat.Instance, new AniEncoder());
        configuration.ImageFormatsManager.SetDecoder(AniFormat.Instance, AniDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new AniImageFormatDetector());
    }
}
