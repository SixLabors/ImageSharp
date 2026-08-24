// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Registers the image encoder, decoder, and format detector for the CUR format.
/// </summary>
public sealed class CurConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetEncoder(CurFormat.Instance, new CurEncoder());
        configuration.ImageFormatsManager.SetDecoder(CurFormat.Instance, CurDecoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new CurImageFormatDetector());
    }
}
