// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the qoi format.
/// </summary>
public sealed class QoiConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetDecoder(QoiFormat.Instance, QoiDecoder.Instance);
        configuration.ImageFormatsManager.SetEncoder(QoiFormat.Instance, new QoiEncoder());
        configuration.ImageFormatsManager.AddImageFormatDetector(new QoiImageFormatDetector());
    }
}
