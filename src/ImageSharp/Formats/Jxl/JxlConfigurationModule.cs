// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Registers the JPEG XL encoders, decoders and mime type detectors.
/// </summary>
public sealed class JxlConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetDecoder(JxlFormat.Instance, JxlDecoder.Instance);
        configuration.ImageFormatsManager.SetEncoder(JxlFormat.Instance, new JxlEncoder());
        configuration.ImageFormatsManager.AddImageFormatDetector(new JxlImageFormatDetector());
    }
}
