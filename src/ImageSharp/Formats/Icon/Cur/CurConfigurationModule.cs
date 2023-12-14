// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the Ico format.
/// </summary>
public sealed class CurConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        // TODO: CurEncoder
        // configuration.ImageFormatsManager.SetEncoder(CurFormat.Instance, new CurEncoder());
        configuration.ImageFormatsManager.SetDecoder(CurFormat.Instance, CurDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new IconImageFormatDetector());
    }
}
