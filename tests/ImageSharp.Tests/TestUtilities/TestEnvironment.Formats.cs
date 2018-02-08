// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests
{
    public static partial class TestEnvironment
    {
        private static Lazy<Configuration> configuration = new Lazy<Configuration>(CreateDefaultConfiguration);

        internal static Configuration Configuration => configuration.Value;
        
        internal static IImageDecoder GetReferenceDecoder(string filePath)
        {
            IImageFormat format = GetImageFormat(filePath);
            return Configuration.FindDecoder(format);
        }

        internal static IImageEncoder GetReferenceEncoder(string filePath)
        {
            IImageFormat format = GetImageFormat(filePath);
            return Configuration.FindEncoder(format);
        }

        internal static IImageFormat GetImageFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension[0] == '.') extension = extension.Substring(1);
            IImageFormat format = Configuration.FindFormatByFileExtension(extension);
            return format;
        }

        private static void ConfigureCodecs(
            this Configuration cfg,
            IImageFormat imageFormat,
            IImageDecoder decoder,
            IImageEncoder encoder,
            IImageFormatDetector detector)
        {
            cfg.SetDecoder(imageFormat, decoder);
            cfg.SetEncoder(imageFormat, encoder);
            cfg.AddImageFormatDetector(detector);
        }

        private static Configuration CreateDefaultConfiguration()
        {
            var configuration = new Configuration(
                new PngConfigurationModule(),
                new JpegConfigurationModule(),
                new GifConfigurationModule()
            );

            if (!IsLinux)
            {
                configuration.ConfigureCodecs(
                    ImageFormats.Png,
                    SystemDrawingReferenceDecoder.Instance,
                    SystemDrawingReferenceEncoder.Png,
                    new PngImageFormatDetector());

                configuration.ConfigureCodecs(
                    ImageFormats.Bmp,
                    SystemDrawingReferenceDecoder.Instance,
                    SystemDrawingReferenceEncoder.Png,
                    new PngImageFormatDetector());
            }
            else
            {
                configuration.Configure(new PngConfigurationModule());
                configuration.Configure(new BmpConfigurationModule());
            }

            return configuration;
        }
    }
}