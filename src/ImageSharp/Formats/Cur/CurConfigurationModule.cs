// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the Ico format.
/// </summary>
public sealed class CurConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(CurFormat.Instance, new CurEncoder());
        configuration.ImageFormatsManager.SetDecoder(CurFormat.Instance, CurDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new IconImageFormatDetector());
    }
}
