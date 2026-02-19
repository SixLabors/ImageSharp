// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests;

public static partial class TestEnvironment
{
    private static readonly Lazy<Configuration> ConfigurationLazy = new(CreateDefaultConfiguration);

    internal static Configuration Configuration => ConfigurationLazy.Value;

    internal static IImageDecoder GetReferenceDecoder(string filePath)
    {
        IImageFormat format = GetImageFormat(filePath);
        return Configuration.ImageFormatsManager.GetDecoder(format);
    }

    internal static IImageEncoder GetReferenceEncoder(string filePath)
    {
        IImageFormat format = GetImageFormat(filePath);
        return Configuration.ImageFormatsManager.GetEncoder(format);
    }

    internal static IImageFormat GetImageFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        Configuration.ImageFormatsManager.TryFindFormatByFileExtension(extension, out IImageFormat format);

        return format;
    }

    private static void ConfigureCodecs(
        this Configuration cfg,
        IImageFormat imageFormat,
        IImageDecoder decoder,
        IImageEncoder encoder,
        IImageFormatDetector detector)
    {
        cfg.ImageFormatsManager.SetDecoder(imageFormat, decoder);
        cfg.ImageFormatsManager.SetEncoder(imageFormat, encoder);
        cfg.ImageFormatsManager.AddImageFormatDetector(detector);
    }

    private static Configuration CreateDefaultConfiguration()
    {
        Configuration cfg = new(
            new JpegConfigurationModule(),
            new GifConfigurationModule(),
            new HeifConfigurationModule(),
            new PbmConfigurationModule(),
            new TgaConfigurationModule(),
            new WebpConfigurationModule(),
            new TiffConfigurationModule(),
            new QoiConfigurationModule());

        IImageEncoder pngEncoder = IsWindows ? SystemDrawingReferenceEncoder.Png : new ImageSharpPngEncoderWithDefaultConfiguration();
        IImageEncoder bmpEncoder = IsWindows ? SystemDrawingReferenceEncoder.Bmp : new BmpEncoder();

        // Magick codecs should work on all platforms
        cfg.ConfigureCodecs(
            PngFormat.Instance,
            MagickReferenceDecoder.Png,
            pngEncoder,
            new PngImageFormatDetector());

        cfg.ConfigureCodecs(
            BmpFormat.Instance,
            IsWindows ? SystemDrawingReferenceDecoder.Bmp : MagickReferenceDecoder.Bmp,
            bmpEncoder,
            new BmpImageFormatDetector());

        return cfg;
    }
}
