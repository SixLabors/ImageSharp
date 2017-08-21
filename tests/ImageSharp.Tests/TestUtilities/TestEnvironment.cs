// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests
{
    using System.Runtime.InteropServices;
    using SixLabors.ImageSharp.Formats.Bmp;
    using SixLabors.ImageSharp.Formats.Gif;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Png;

    public static class TestEnvironment
    {
        private const string ImageSharpSolutionFileName = "ImageSharp.sln";

        private const string InputImagesRelativePath = @"tests\Images\Input";

        private const string ActualOutputDirectoryRelativePath = @"tests\Images\ActualOutput";

        private const string ReferenceOutputDirectoryRelativePath = @"tests\Images\External\ReferenceOutput";

        private static Lazy<string> solutionDirectoryFullPath = new Lazy<string>(GetSolutionDirectoryFullPathImpl);

        private static Lazy<bool> runsOnCi = new Lazy<bool>(
            () =>
                {
                    bool isCi;
                    return bool.TryParse(Environment.GetEnvironmentVariable("CI"), out isCi) && isCi;
                });

        private static Lazy<Configuration> configuration = new Lazy<Configuration>(CreateDefaultConfiguration);
        
        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Gets a value indicating whether test execution runs on CI.
        /// </summary>
        internal static bool RunsOnCI => runsOnCi.Value;

        internal static string SolutionDirectoryFullPath => solutionDirectoryFullPath.Value;

        internal static Configuration Configuration => configuration.Value;

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

        private static string GetSolutionDirectoryFullPathImpl()
        {
            string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;

            var assemblyFile = new FileInfo(assemblyLocation);

            DirectoryInfo directory = assemblyFile.Directory;

            while (!directory.EnumerateFiles(ImageSharpSolutionFileName).Any())
            {
                try
                {
                    directory = directory.Parent;
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Unable to find ImageSharp solution directory from {assemblyLocation} because of {ex.GetType().Name}!",
                        ex);
                }
                if (directory == null)
                {
                    throw new Exception($"Unable to find ImageSharp solution directory from {assemblyLocation}!");
                }
            }

            return directory.FullName;
        }

        /// <summary>
        /// Gets the correct full path to the Input Images directory.
        /// </summary>
        internal static string InputImagesDirectoryFullPath =>
            Path.Combine(SolutionDirectoryFullPath, InputImagesRelativePath).Replace('\\', Path.DirectorySeparatorChar);

        /// <summary>
        /// Gets the correct full path to the Actual Output directory. (To be written to by the test cases.)
        /// </summary>
        internal static string ActualOutputDirectoryFullPath => Path.Combine(
            SolutionDirectoryFullPath,
            ActualOutputDirectoryRelativePath).Replace('\\', Path.DirectorySeparatorChar);

        /// <summary>
        /// Gets the correct full path to the Expected Output directory. (To compare the test results to.)
        /// </summary>
        internal static string ReferenceOutputDirectoryFullPath => Path.Combine(
            SolutionDirectoryFullPath,
            ReferenceOutputDirectoryRelativePath).Replace('\\', Path.DirectorySeparatorChar);

        internal static string GetReferenceOutputFileName(string actualOutputFileName) =>
            actualOutputFileName.Replace("ActualOutput", @"External\ReferenceOutput").Replace('\\', Path.DirectorySeparatorChar);

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

        internal static bool IsLinux =>
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }
}